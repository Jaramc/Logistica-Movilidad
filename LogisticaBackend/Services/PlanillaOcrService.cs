using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Options;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

/// <summary>
/// Servicio de digitalización OCR que interactúa con Azure AI Document Intelligence.
/// Inyecta DocumentAnalysisClient como Singleton y garantiza cálculos contables autoritativos.
/// </summary>
public class PlanillaOcrService : IPlanillaOcrService
{
    private readonly DocumentAnalysisClient _client;
    private readonly AzureOcrOptions _options;
    private readonly ILogger<PlanillaOcrService> _logger;

    public PlanillaOcrService(
        DocumentAnalysisClient client,
        IOptions<AzureOcrOptions> options,
        ILogger<PlanillaOcrService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlanillaOcrResponseDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var modelId = string.IsNullOrWhiteSpace(_options.ModelId) ? "planillas-gastos-v1" : _options.ModelId;
        _logger.LogInformation("Iniciando análisis de planilla con Azure Document Intelligence (Modelo: {ModelId}).", modelId);

        AnalyzeResult analyzeResult;
        try
        {
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                modelId,
                stream,
                new AnalyzeDocumentOptions(),
                cancellationToken
            );

            analyzeResult = operation.Value;
        }
        catch (RequestFailedException rfEx)
        {
            // Solo el código y el estado: la excepción completa incluye el cuerpo de la respuesta de Azure.
            _logger.LogWarning("Azure Document Intelligence rechazó el análisis. Código: {ErrorCode}. Estado: {Status}.", rfEx.ErrorCode, rfEx.Status);
            throw;
        }

        var advertencias = new List<string>();

        if (analyzeResult?.Documents == null || analyzeResult.Documents.Count == 0)
        {
            _logger.LogWarning("Azure no detectó ningún documento estructurado en el archivo enviado.");
            advertencias.Add("No se detectaron campos legibles en el documento.");

            return new PlanillaOcrResponseDto(
                "No se detectó información estructurada en el documento.",
                new PlanillaOcrDataDto(
                    Placa: null,
                    Origen: null,
                    Destino: null,
                    Flete: 0m,
                    Anticipo: 0m,
                    TotalGastos: 0m,
                    SaldoViaje: 0m,
                    DetalleGastos: new List<GastoDetalleOcrDto>(),
                    Advertencias: advertencias
                )
            );
        }

        var document = analyzeResult.Documents[0];
        var fields = document.Fields ?? new Dictionary<string, DocumentField>();

        // 1. Extracción tolerante a nulos de campos dedicados principales
        var placa = ExtraerTextoSeguro(fields, "Placa")?.ToUpperInvariant();
        var origen = ExtraerTextoSeguro(fields, "Origen");
        var destino = ExtraerTextoSeguro(fields, "Destino");
        var fleteDedicado = ExtraerNumeroSeguro(fields, "Flete");
        var anticipoDedicado = ExtraerNumeroSeguro(fields, "Anticipo");
        var totalGastosAzure = ExtraerNumeroSeguro(fields, "TotalGastos");
        var saldoViajeAzure = ExtraerNumeroSeguro(fields, "SaldoViaje");

        // Campos de cabecera opcionales
        var conductor = ExtraerTextoSeguro(fields, "Conductor");
        var manifiesto = ExtraerTextoSeguro(fields, "Manifiesto");
        var empresa = ExtraerTextoSeguro(fields, "EmpresaTransportadora") ?? ExtraerTextoSeguro(fields, "Empresa");
        var fechaInicial = ExtraerFechaSegura(fields, "FechaInicial");
        var fechaFinal = ExtraerFechaSegura(fields, "FechaFinal");

        // 2. Extracción de la tabla de gastos con filtrado inteligente de Flete y Anticipo
        var gastosExtraidos = ExtraerTablaGastos(document);
        var gastosFinales = new List<GastoDetalleOcrDto>();

        decimal fleteFinal = fleteDedicado;
        decimal anticipoFinal = anticipoDedicado;

        foreach (var item in gastosExtraidos)
        {
            var conceptoNorm = item.Concepto.Trim().ToUpperInvariant();

            // Filtrado de fila FLETE dentro de la tabla de gastos
            if (conceptoNorm == "FLETE" || conceptoNorm.StartsWith("FLETE "))
            {
                if (fleteFinal == 0m && item.Valor > 0m)
                {
                    fleteFinal = item.Valor;
                }
                else if (fleteFinal > 0m && item.Valor > 0m && fleteFinal != item.Valor)
                {
                    advertencias.Add($"El flete en cabecera (${fleteFinal:N0}) difiere del flete en la tabla (${item.Valor:N0}). Se conserva el valor de cabecera.");
                }
                // Se descarta de la lista de gastos para evitar doble conteo
                continue;
            }

            // Filtrado de fila ANTICIPO dentro de la tabla de gastos
            if (conceptoNorm == "ANTICIPO" || conceptoNorm.StartsWith("ANTICIPO "))
            {
                if (anticipoFinal == 0m && item.Valor > 0m)
                {
                    anticipoFinal = item.Valor;
                }
                else if (anticipoFinal > 0m && item.Valor > 0m && anticipoFinal != item.Valor)
                {
                    advertencias.Add($"El anticipo en cabecera (${anticipoFinal:N0}) difiere del anticipo en la tabla (${item.Valor:N0}). Se conserva el valor de cabecera.");
                }
                // Se descarta de la lista de gastos para evitar doble conteo
                continue;
            }

            gastosFinales.Add(item);
        }

        // 3. Los totales y saldos oficiales proceden de CalculadoraLiquidacion (NO del OCR ciego)
        var calculoContable = CalculadoraLiquidacion.Calcular(
            fleteFinal,
            anticipoFinal,
            gastosFinales.Select(g => g.Valor)
        );

        var totalGastosCalculado = calculoContable.TotalGastos;
        var saldoViajeCalculado = calculoContable.SaldoViaje;

        // Comprobación de advertencias contra los valores leídos por Azure
        if (totalGastosAzure > 0m && Math.Abs(totalGastosAzure - totalGastosCalculado) > 0.01m)
        {
            advertencias.Add($"El Total Gastos impreso en el comprobante (${totalGastosAzure:N0}) difiere de la suma de rubros identificados (${totalGastosCalculado:N0}).");
        }

        if (saldoViajeAzure != 0m && Math.Abs(saldoViajeAzure - saldoViajeCalculado) > 0.01m)
        {
            advertencias.Add($"El Saldo Viaje impreso en el comprobante (${saldoViajeAzure:N0}) difiere del balance calculado (${saldoViajeCalculado:N0}).");
        }

        var dataDto = new PlanillaOcrDataDto(
            Placa: placa,
            Origen: origen,
            Destino: destino,
            Flete: fleteFinal,
            Anticipo: anticipoFinal,
            TotalGastos: totalGastosCalculado,
            SaldoViaje: saldoViajeCalculado,
            DetalleGastos: gastosFinales,
            Conductor: conductor,
            FechaInicial: fechaInicial,
            FechaFinal: fechaFinal,
            Manifiesto: manifiesto,
            EmpresaTransportadora: empresa,
            Advertencias: advertencias.Count > 0 ? advertencias : null
        );

        return new PlanillaOcrResponseDto("Planilla procesada exitosamente.", dataDto);
    }

    #region Métodos de Extracción Tolerantes a Nulos

    private static DocumentField? BuscarCampoCaseInsensitive(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo)
    {
        if (fields == null) return null;
        var key = fields.Keys.FirstOrDefault(k => string.Equals(k, nombreCampo, StringComparison.OrdinalIgnoreCase));
        return key != null && fields.TryGetValue(key, out var field) ? field : null;
    }

    private static string? ExtraerTextoSeguro(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo)
    {
        try
        {
            var field = BuscarCampoCaseInsensitive(fields, nombreCampo);
            if (field == null) return null;

            if (!string.IsNullOrWhiteSpace(field.Content))
                return field.Content.Trim();

            if (field.FieldType == DocumentFieldType.String)
                return field.Value?.AsString()?.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static decimal ExtraerNumeroSeguro(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo)
    {
        try
        {
            var field = BuscarCampoCaseInsensitive(fields, nombreCampo);
            if (field == null) return 0m;

            if (field.FieldType == DocumentFieldType.Double)
                return CalculadoraLiquidacion.Redondear((decimal)field.Value.AsDouble());

            if (field.FieldType == DocumentFieldType.Int64)
                return (decimal)field.Value.AsInt64();

            if (field.FieldType == DocumentFieldType.Currency)
                return CalculadoraLiquidacion.Redondear((decimal)field.Value.AsCurrency().Amount);

            return CalculadoraLiquidacion.ParsearMonedaColombiana(field.Content);
        }
        catch
        {
            return 0m;
        }
    }

    private static DateOnly? ExtraerFechaSegura(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo)
    {
        try
        {
            var field = BuscarCampoCaseInsensitive(fields, nombreCampo);
            if (field == null) return null;

            if (field.FieldType == DocumentFieldType.Date)
            {
                var dt = field.Value.AsDate();
                return new DateOnly(dt.Year, dt.Month, dt.Day);
            }

            if (!string.IsNullOrWhiteSpace(field.Content) && DateTime.TryParse(field.Content, out var parsed))
            {
                return DateOnly.FromDateTime(parsed);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private List<GastoDetalleOcrDto> ExtraerTablaGastos(AnalyzedDocument document)
    {
        var lista = new List<GastoDetalleOcrDto>();
        if (document?.Fields == null) return lista;

        try
        {
            var tablaField = BuscarCampoCaseInsensitive(document.Fields, "DetalleGastos") ?? BuscarCampoCaseInsensitive(document.Fields, "Gastos");
            if (tablaField == null || tablaField.FieldType != DocumentFieldType.List)
                return lista;

            var filas = tablaField.Value?.AsList();
            if (filas == null) return lista;

            foreach (var filaField in filas)
            {
                if (filaField == null || filaField.FieldType != DocumentFieldType.Dictionary)
                    continue;

                try
                {
                    var columnas = filaField.Value?.AsDictionary();
                    if (columnas == null) continue;

                    var conceptoField = BuscarCampoCaseInsensitive(columnas, "Concepto");
                    var valorField = BuscarCampoCaseInsensitive(columnas, "Valor");

                    string concepto = string.Empty;
                    if (conceptoField != null)
                    {
                        if (!string.IsNullOrWhiteSpace(conceptoField.Content))
                            concepto = conceptoField.Content.Trim();
                        else if (conceptoField.FieldType == DocumentFieldType.String)
                            concepto = conceptoField.Value?.AsString()?.Trim() ?? string.Empty;
                    }

                    decimal valor = 0m;
                    if (valorField != null)
                    {
                        if (valorField.FieldType == DocumentFieldType.Double)
                            valor = (decimal)valorField.Value.AsDouble();
                        else if (valorField.FieldType == DocumentFieldType.Int64)
                            valor = (decimal)valorField.Value.AsInt64();
                        else if (valorField.FieldType == DocumentFieldType.Currency)
                            valor = (decimal)valorField.Value.AsCurrency().Amount;
                        else
                            valor = CalculadoraLiquidacion.ParsearMonedaColombiana(valorField.Content);
                    }

                    valor = CalculadoraLiquidacion.Redondear(valor);

                    // Descarta filas completamente vacías
                    if (!string.IsNullOrWhiteSpace(concepto) || valor > 0m)
                    {
                        lista.Add(new GastoDetalleOcrDto(
                            Concepto: string.IsNullOrWhiteSpace(concepto) ? "Gasto no identificado" : concepto,
                            Valor: valor
                        ));
                    }
                }
                catch (Exception exFila)
                {
                    _logger.LogWarning(exFila, "Error al procesar fila individual de gastos en OCR.");
                }
            }
        }
        catch (Exception exTabla)
        {
            _logger.LogError(exTabla, "Error al extraer la tabla DetalleGastos con Azure.");
        }

        return lista;
    }

    #endregion
}
