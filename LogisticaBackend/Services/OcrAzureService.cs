using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

public class OcrAzureService : IOcrAzureService, TuProyecto.Application.Interfaces.IOcrAzureService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OcrAzureService> _logger;

    public OcrAzureService(IConfiguration configuration, ILogger<OcrAzureService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<object> ProcesarDocumentoAsync(Stream stream, string modelId)
    {
        var endpoint = _configuration["Azure:FormRecognizer:Endpoint"];
        var apiKey = _configuration["Azure:FormRecognizer:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Las credenciales de Azure Form Recognizer no están configuradas en appsettings.json. Retornando respuesta simulada.");
            return new
            {
                advertencia = "Azure Form Recognizer no configurado. Configure 'Azure:FormRecognizer:Endpoint' y 'ApiKey' en appsettings.json.",
                campos = new Dictionary<string, object?>
                {
                    { "MerchantName", "Peaje / Comercio Simulado" },
                    { "Total", 15000.00 },
                    { "TransactionDate", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                }
            };
        }

        var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        
        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);
        var result = operation.Value;

        var documentosExtraidos = new List<Dictionary<string, object?>>();

        foreach (var document in result.Documents)
        {
            var campos = new Dictionary<string, object?>();
            foreach (var (nombreCampo, valorCampo) in document.Fields)
            {
                campos[nombreCampo] = valorCampo.Content;
            }
            documentosExtraidos.Add(campos);
        }

        return new
        {
            totalDocumentos = result.Documents.Count,
            documentos = documentosExtraidos,
            contenidoCrudo = result.Content
        };
    }
}
