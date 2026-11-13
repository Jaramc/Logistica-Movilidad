namespace LogisticaBackend.Interfaces;

public interface IExcelExportService
{
    Task<MemoryStream> GenerarConsolidadoExcelAsync(
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken cancellationToken = default);
}

