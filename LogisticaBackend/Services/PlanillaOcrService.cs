using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

public class PlanillaOcrService : IPlanillaOcrService
{
    private const string ModelId = "planillas-gastos-v1";
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<PlanillaOcrService> _logger;

    public PlanillaOcrService(IConfiguration configuration, ILogger<PlanillaOcrService> logger)
    {
        _logger = logger;
        var endpoint = configuration["AzureDocumentIntelligence:Endpoint"]
            ?? configuration["Azure:FormRecognizer:Endpoint"];
        var apiKey = configuration["AzureDocumentIntelligence:ApiKey"]
            ?? configuration["Azure:FormRecognizer:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Las credenciales de Azure Document Intelligence no están configuradas.");
        }

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<PlanillaOcrResultDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var resultado = new PlanillaOcrResultDto();
        
        try
        {
            _logger.LogInformation("Enviando documento a Azure Document Intelligence con modelo '{ModelId}'.", ModelId);

            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                ModelId,
                stream,
                new AnalyzeDocumentOptions(),
                cancellationToken
            );

            var analyzeResult = operation.Value;
            if (analyzeResult?.Documents == null || analyzeResult.Documents.Count == 0)
            {
                _logger.LogWarning("No se encontraron documentos estructurados en la respuesta de Azure.");
                return resultado;
            }

            var document = analyzeResult.Documents[0];
            var fields = document.Fields;

            if (fields != null)
            {
                // 1. Extracción de campos fijos (ignorando mayúsculas/minúsculas)
                resultado.Placa = ExtraerTextoSeguro(fields, "Placa");
                resultado.Origen = ExtraerTextoSeguro(fields, "Origen");
                resultado.Destino = ExtraerTextoSeguro(fields, "Destino");
                resultado.Flete = ExtraerNumeroSeguro(fields, "Flete");
                resultado.Anticipo = ExtraerNumeroSeguro(fields, "Anticipo");
                resultado.TotalGastos = ExtraerNumeroSeguro(fields, "TotalGastos");
                resultado.SaldoViaje = ExtraerNumeroSeguro(fields, "SaldoViaje");

                // 2. Extracción de la tabla dinámica
                resultado.DetalleGastos = ExtraerTablaDetalleGastos(document);
            }

            return resultado;
        }
        catch (RequestFailedException rfEx)
        {
            _logger.LogError(rfEx, "Error devuelto por la API de Azure AI: {ErrorCode} (Status {Status})", rfEx.ErrorCode, rfEx.Status);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado al procesar la planilla en PlanillaOcrService.");
            throw;
        }
    }

    #region Métodos de Extracción Resiliente

    // Busca la llave en el diccionario sin importar si está en MAYÚSCULAS o minúsculas
    private static DocumentField? ObtenerCampoIgnorandoMayusculas(IReadOnlyDictionary<string, DocumentField> fields, string nombreCampo)
    {
        if (fields == null) return null;
        var key = fields.Keys.FirstOrDefault(k => string.Equals(k, nombreCampo, StringComparison.OrdinalIgnoreCase));
        return key != null && fields.ContainsKey(key) ? fields[key] : null;
    }

    private static string? ExtraerTextoSeguro(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        try
        {
            var field = ObtenerCampoIgnorandoMayusculas(fields, fieldName);
            if (field == null) return null;
            if (!string.IsNullOrWhiteSpace(field.Content)) return field.Content.Trim();
            if (field.FieldType == DocumentFieldType.String) return field.Value?.AsString()?.Trim();
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static double ExtraerNumeroSeguro(IReadOnlyDictionary<string, DocumentField> fields, string fieldName)
    {
        try
        {
            var field = ObtenerCampoIgnorandoMayusculas(fields, fieldName);
            return ParsearNumeroColombiano(field);
        }
        catch
        {
            return 0.0;
        }
    }

    private static double ParsearNumeroColombiano(DocumentField? field)
    {
        if (field == null) return 0.0;

        try
        {
            if (field.FieldType == DocumentFieldType.Double)
                return field.Value.AsDouble();

            if (field.FieldType == DocumentFieldType.Currency)
                return field.Value.AsCurrency().Amount;

            if (field.FieldType == DocumentFieldType.Int64)
                return (double)field.Value.AsInt64();

            if (string.IsNullOrWhiteSpace(field.Content)) 
                return 0.0;

            // Elimina signos de moneda, puntos y espacios, quedándose con los dígitos
            var soloNumeros = Regex.Replace(field.Content, @"[^\d]", string.Empty);
            
            if (double.TryParse(soloNumeros, out var valor))
            {
                return valor;
            }
            return 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private List<GastoDetalleDto> ExtraerTablaDetalleGastos(AnalyzedDocument document)
    {
        var listaGastos = new List<GastoDetalleDto>();
        if (document?.Fields == null) return listaGastos;

        try
        {
            var tablaField = ObtenerCampoIgnorandoMayusculas(document.Fields, "DetalleGastos");

            if (tablaField != null && tablaField.FieldType == DocumentFieldType.List)
            {
                var filas = tablaField.Value?.AsList();
                if (filas == null) return listaGastos;

                foreach (var filaField in filas)
                {
                    if (filaField == null) continue;

                    try
                    {
                        if (filaField.FieldType == DocumentFieldType.Dictionary)
                        {
                            var columnas = filaField.Value?.AsDictionary();
                            if (columnas == null) continue;
                            
                            var conceptoField = ObtenerCampoIgnorandoMayusculas(columnas, "Concepto");
                            var valorField = ObtenerCampoIgnorandoMayusculas(columnas, "Valor");

                            // Null-safe para evitar NullReferenceException si Content es null
                            string concepto = string.Empty;
                            if (conceptoField != null)
                            {
                                if (!string.IsNullOrWhiteSpace(conceptoField.Content))
                                    concepto = conceptoField.Content.Trim();
                                else if (conceptoField.FieldType == DocumentFieldType.String)
                                    concepto = conceptoField.Value?.AsString()?.Trim() ?? string.Empty;
                            }

                            var valor = ParsearNumeroColombiano(valorField);

                            if (!string.IsNullOrWhiteSpace(concepto) || valor > 0)
                            {
                                listaGastos.Add(new GastoDetalleDto
                                {
                                    Concepto = concepto,
                                    Valor = valor
                                });
                            }
                        }
                    }
                    catch (Exception exFila)
                    {
                        _logger.LogWarning(exFila, "Error al extraer una fila de la tabla de gastos. Se continuará con las siguientes.");
                    }
                }
            }
        }
        catch (Exception exTabla)
        {
            _logger.LogError(exTabla, "Error general al procesar la tabla de DetalleGastos.");
        }

        return listaGastos;
    }

    #endregion
}
