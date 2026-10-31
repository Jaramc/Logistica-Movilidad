namespace TuProyecto.Application.Interfaces
{
    public interface IOcrAzureService
    {
        Task<object> ProcesarDocumentoAsync(Stream stream, string modelId);
    }
}

namespace LogisticaBackend.Interfaces
{
    public interface IOcrAzureService : TuProyecto.Application.Interfaces.IOcrAzureService
    {
    }
}
