using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

public class OcrAzureService : IOcrAzureService, TuProyecto.Application.Interfaces.IOcrAzureService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<OcrAzureService> _logger;

    public OcrAzureService(DocumentAnalysisClient client, ILogger<OcrAzureService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<object> ProcesarDocumentoAsync(Stream stream, string modelId)
    {
        var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, stream);
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
