using LogisticaBackend.DTOs;

namespace LogisticaBackend.Interfaces;

public interface IPlanillaOcrService
{
    /// <summary>
    /// Procesa el stream de una planilla física utilizando el modelo personalizado de Azure Document Intelligence.
    /// </summary>
    /// <param name="stream">Flujo binario del documento (imagen/PDF).</param>
    /// <param name="cancellationToken">Token de cancelación opcional.</param>
    /// <returns>Objeto estructurado con los campos fijos y la tabla de gastos extraída.</returns>
    Task<PlanillaOcrResultDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default);
}

