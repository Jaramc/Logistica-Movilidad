using LogisticaBackend.DTOs;

namespace LogisticaBackend.Interfaces;

public interface IPlanillaOcrService
{
    Task<PlanillaOcrResponseDto> ProcesarPlanillaAsync(Stream stream, CancellationToken cancellationToken = default);
}
