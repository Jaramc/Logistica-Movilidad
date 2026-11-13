using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LogisticaBackend.Data;

/// <summary>Comprueba que PostgreSQL responde. No expone detalles del error.</summary>
public sealed class BaseDatosHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public BaseDatosHealthCheck(AppDbContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
