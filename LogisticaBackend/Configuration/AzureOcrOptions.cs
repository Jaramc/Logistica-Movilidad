using System.ComponentModel.DataAnnotations;

namespace LogisticaBackend.Configuration;

public class AzureOcrOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    [Required(ErrorMessage = "El Endpoint de Azure Document Intelligence es obligatorio.")]
    [Url(ErrorMessage = "El Endpoint de Azure Document Intelligence debe ser una URL válida.")]
    public string Endpoint { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ApiKey de Azure Document Intelligence es obligatoria.")]
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "planillas-gastos-v1";
}

