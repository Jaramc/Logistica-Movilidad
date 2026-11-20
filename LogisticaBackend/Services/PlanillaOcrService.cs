using System.Text.Json;
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
    /// <summary>Campos del modelo custom que la UI espera. Los que falten se reportan en CamposNoEncontrados.</summary>
    public static readonly IReadOnlyList<string> CamposEsperados = new[]
    {
        "Placa", "Origen", "Destino", "Flete", "Anticipo", "TotalGastos", "SaldoViaje", "DetalleGastos"
    };

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

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

    /// <summary>Un campo tal como lo leyó Azure: lo que se guarda en jsonOcrCrudo.</summary>
    private sealed record CampoLeido(string Nombre, string? Content, float? Confidence);

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
                    Advertencias: advertencias,
                    ConfianzaPromedio: null,
                    CamposNoEncontrados: CamposEsperados.ToList(),
                    JsonOcrCrudo: "[]"
                )
            );
        }

        var document = analyzeResult.Documents[0];
        var fields = document.Fields ?? new Dictionary<string, DocumentField>();
        var leidos = new List<CampoLeido>();
        var noEncontrados = new List<string>();

        // 1. Campos de cabecera
        var placa = ExtraerTexto(fields, "Placa", leidos, noEncontrados)?.ToUpperInvariant();
        var origen = ExtraerTexto(fields, "Origen", leidos, noEncontrados);
        var destino = ExtraerTexto(fields, "Destino", leidos, noEncontrados);
        var fleteDedicado = ExtraerNumero(fields, "Flete", leidos, noEncontrados);
        var anticipoDedicado = ExtraerNumero(fields, "Anticipo", leidos, noEncontrados);
        var totalGastosAzure = ExtraerNumero(fields, "TotalGastos", leidos, noEncontrados);
        var saldoViajeAzure = ExtraerNumero(fields, "SaldoViaje", leidos, noEncontrados);

        // Campos opcionales: no se reportan como faltantes (no son parte del modelo custom).
        var conductor = ExtraerTexto(fields, "Conductor", leidos, null);
        var manifiesto = ExtraerTexto(fields, "Manifiesto", leidos, null);
        var empresa = ExtraerTexto(fields, "EmpresaTransportadora", leidos, null) ?? ExtraerTexto(fields, "Empresa", leidos, null);
        var fechaInicial = ExtraerFecha(fields, "FechaInicial", leidos);
        var fechaFinal = ExtraerFecha(fields, "FechaFinal", leidos);

        // 2. Tabla de gastos, con filtrado de los renglones FLETE y ANTICIPO
        var gastosExtraidos = ExtraerTablaGastos(document, leidos, noEncontrados);
        var gastosFinales = new List<GastoDetalleOcrDto>();

        decimal fleteFinal = fleteDedicado;
        decimal anticipoFinal = anticipoDedicado;

        foreach (var item in gastosExtraidos)
        {
            var conceptoNorm = item.Concepto.Trim().ToUpperInvariant();

            if (conceptoNorm == "FLETE" || conceptoNorm.StartsWith("FLETE "))
            {
                if (fleteFinal == 0m && item.Valor > 0m)
                {
                    fleteFinal = item.Valor;
                    noEncontrados.Remove("Flete");
                }
                else if (fleteFinal > 0m && item.Valor > 0m && fleteFinal != item.Valor)
                {
                    advertencias.Add($"El flete en cabecera (${fleteFinal:N0}) difiere del flete en la tabla (${item.Valor:N0}). Se conserva el valor de cabecera.");
                }
                // Se descarta de la lista de gastos para evitar doble conteo
                continue;
            }

            if (conceptoNorm == "ANTICIPO" || conceptoNorm.StartsWith("ANTICIPO "))
            {
                if (anticipoFinal == 0m && item.Valor > 0m)
                {
                    anticipoFinal = item.Valor;
                    noEncontrados.Remove("Anticipo");
                }
                else if (anticipoFinal > 0m && item.Valor > 0m && anticipoFinal != item.Valor)
                {
                    advertencias.Add($"El anticipo en cabecera (${anticipoFinal:N0}) difiere del anticipo en la tabla (${item.Valor:N0}). Se conserva el valor de cabecera.");
                }
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

        if (totalGastosAzure > 0m && Math.Abs(totalGastosAzure - totalGastosCalculado) > 0.01m)
        {
            advertencias.Add($"El Total Gastos impreso en el comprobante (${totalGastosAzure:N0}) difiere de la suma de rubros identificados (${totalGastosCalculado:N0}).");
        }

        if (saldoViajeAzure != 0m && Math.Abs(saldoViajeAzure - saldoViajeCalculado) > 0.01m)
        {
            advertencias.Add($"El Saldo Viaje impreso en el comprobante (${saldoViajeAzure:N0}) difiere del balance calculado (${saldoViajeCalculado:N0}).");
        }

        var confianzas = leidos.Where(c => c.Confidence.HasValue).Select(c => c.Confidence!.Value).ToList();
        float? confianzaPromedio = confianzas.Count > 0 ? (float)Math.Round(confianzas.Average(), 4) : null;

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
            Advertencias: advertencias.Count > 0 ? advertencias : null,
            ConfianzaPromedio: confianzaPromedio,
            CamposNoEncontrados: noEncontrados,
            JsonOcrCrudo: JsonSerializer.Serialize(leidos, OpcionesJson)
        );

        return new PlanillaOcrResponseDto("Planilla procesada exitosamente.", dataDto);
    }

    #region Extracción de campos

    private static DocumentField? BuscarCampo(IReadOnlyDictionary<string, DocumentField>? fields, string nombreCampo)
    {
        if (fields == null) return null;
        var key = fields.Keys.FirstOrDefault(k => string.Equals(k, nombreCampo, StringComparison.OrdinalIgnoreCase));
        return key != null && fields.TryGetValue(key, out var field) ? field : null;
    }

    /// <summary>Errores esperables al leer un DocumentField: tipo distinto al pedido o valor fuera de rango.</summary>
    private static bool EsErrorDeLectura(Exception ex) =>
        ex is InvalidOperationException or FormatException or OverflowException or ArgumentException;

    private string? LeerTexto(DocumentField field, string nombreCampo)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(field.Content))
                return field.Content.Trim();

            return field.FieldType == DocumentFieldType.String ? field.Value?.AsString()?.Trim() : null;
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como texto; se aplica el valor por defecto {ValorPorDefecto}.", nombreCampo, "null");
            return null;
        }
    }

    /// <summary>Valor tipado (Currency/Double/Int64) convertido a decimal; el texto solo se parsea si el tipo es String o desconocido.</summary>
    private decimal LeerNumero(DocumentField field, string nombreCampo)
    {
        try
        {
            var valor = field.FieldType switch
            {
                DocumentFieldType.Currency => (decimal)field.Value.AsCurrency().Amount,
                DocumentFieldType.Double => (decimal)field.Value.AsDouble(),
                DocumentFieldType.Int64 => field.Value.AsInt64(),
                _ => MontoParser.ParseCop(field.Content)
            };
            return CalculadoraLiquidacion.Redondear(valor);
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como monto; se aplica el valor por defecto {ValorPorDefecto}.", nombreCampo, 0m);
            return 0m;
        }
    }

    private string? ExtraerTexto(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo,
        List<CampoLeido> leidos, List<string>? noEncontrados)
    {
        var field = BuscarCampo(fields, nombreCampo);
        var texto = field is null ? null : LeerTexto(field, nombreCampo);
        if (field is not null) leidos.Add(new CampoLeido(nombreCampo, field.Content, field.Confidence));
        if (string.IsNullOrWhiteSpace(texto)) noEncontrados?.Add(nombreCampo);
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }

    private decimal ExtraerNumero(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo,
        List<CampoLeido> leidos, List<string> noEncontrados)
    {
        var field = BuscarCampo(fields, nombreCampo);
        if (field is null || (string.IsNullOrWhiteSpace(field.Content) && field.Value is null))
        {
            if (field is not null) leidos.Add(new CampoLeido(nombreCampo, field.Content, field.Confidence));
            noEncontrados.Add(nombreCampo);
            return 0m;
        }

        leidos.Add(new CampoLeido(nombreCampo, field.Content, field.Confidence));
        return LeerNumero(field, nombreCampo);
    }

    private DateOnly? ExtraerFecha(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo, List<CampoLeido> leidos)
    {
        var field = BuscarCampo(fields, nombreCampo);
        if (field is null) return null;
        leidos.Add(new CampoLeido(nombreCampo, field.Content, field.Confidence));

        try
        {
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
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como fecha; se aplica el valor por defecto {ValorPorDefecto}.", nombreCampo, "null");
            return null;
        }
    }

    private List<GastoDetalleOcrDto> ExtraerTablaGastos(AnalyzedDocument document, List<CampoLeido> leidos, List<string> noEncontrados)
    {
        var lista = new List<GastoDetalleOcrDto>();

        var tablaField = BuscarCampo(document?.Fields, "DetalleGastos") ?? BuscarCampo(document?.Fields, "Gastos");
        if (tablaField == null || tablaField.FieldType != DocumentFieldType.List)
        {
            noEncontrados.Add("DetalleGastos");
            return lista;
        }

        IReadOnlyList<DocumentField>? filas;
        try
        {
            filas = tablaField.Value?.AsList();
        }
        catch (Exception ex) when (EsErrorDeLectura(ex))
        {
            _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como lista; se aplica el valor por defecto {ValorPorDefecto}.", "DetalleGastos", "[]");
            filas = null;
        }

        for (var i = 0; filas != null && i < filas.Count; i++)
        {
            var filaField = filas[i];
            if (filaField == null || filaField.FieldType != DocumentFieldType.Dictionary)
                continue;

            IReadOnlyDictionary<string, DocumentField>? columnas;
            try
            {
                columnas = filaField.Value?.AsDictionary();
            }
            catch (Exception ex) when (EsErrorDeLectura(ex))
            {
                _logger.LogWarning(ex, "No se pudo leer el campo OCR {Campo} como fila; se aplica el valor por defecto {ValorPorDefecto}.", $"DetalleGastos[{i}]", "fila omitida");
                continue;
            }
            if (columnas == null) continue;

            var conceptoField = BuscarCampo(columnas, "Concepto");
            var valorField = BuscarCampo(columnas, "Valor");

            var concepto = conceptoField is null ? string.Empty : LeerTexto(conceptoField, $"DetalleGastos[{i}].Concepto") ?? string.Empty;
            var valor = valorField is null ? 0m : LeerNumero(valorField, $"DetalleGastos[{i}].Valor");

            if (conceptoField is not null) leidos.Add(new CampoLeido($"DetalleGastos[{i}].Concepto", conceptoField.Content, conceptoField.Confidence));
            if (valorField is not null) leidos.Add(new CampoLeido($"DetalleGastos[{i}].Valor", valorField.Content, valorField.Confidence));

            // Descarta filas completamente vacías
            if (string.IsNullOrWhiteSpace(concepto) && valor == 0m)
                continue;

            // La confianza de la fila es la menor de sus dos celdas.
            var confianzas = new[] { conceptoField?.Confidence, valorField?.Confidence }.Where(c => c.HasValue).Select(c => c!.Value).ToList();

            lista.Add(new GastoDetalleOcrDto(
                Concepto: string.IsNullOrWhiteSpace(concepto) ? "Gasto no identificado" : concepto,
                Valor: valor,
                Confianza: confianzas.Count > 0 ? confianzas.Min() : null
            ));
        }

        return lista;
    }

    #endregion
}
