using System.Text.Json;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Services;

/// <summary>Acciones auditadas. Texto estable: se usa para filtrar en la consulta.</summary>
public static class AccionesAuditoria
{
    public const string LoginExitoso = "LoginExitoso";
    public const string LoginFallido = "LoginFallido";
    public const string CuentaBloqueada = "CuentaBloqueada";
    public const string SegundoFactorExitoso = "SegundoFactorExitoso";
    public const string SegundoFactorFallido = "SegundoFactorFallido";
    public const string Logout = "Logout";
    public const string CambioPassword = "CambioPassword";
    public const string DosFactoresConfigurado = "DosFactoresConfigurado";
    public const string DosFactoresReiniciado = "DosFactoresReiniciado";
    public const string CodigosRecuperacionRegenerados = "CodigosRecuperacionRegenerados";
    public const string UsuarioCreado = "UsuarioCreado";
    public const string PasswordTemporalRegenerada = "PasswordTemporalRegenerada";
    public const string RolCambiado = "RolCambiado";
    public const string UsuarioDesactivado = "UsuarioDesactivado";
    public const string UsuarioActivado = "UsuarioActivado";
    public const string PlanillaGuardada = "PlanillaGuardada";
    public const string ConsolidadoDescargado = "ConsolidadoDescargado";
}

public interface IAuditoriaService
{
    /// <summary>Inserta un evento. No existe ninguna operación para modificar o borrar.</summary>
    Task RegistrarAsync(
        string accion,
        bool exito,
        Guid? usuarioId = null,
        string? entidad = null,
        string? entidadId = null,
        object? detalle = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inserta en un DbContext propio (scope nuevo) para no mezclar la auditoría con cambios
/// pendientes de la petición, y para que el evento quede aunque la operación principal falle.
/// </summary>
public sealed class AuditoriaService : IAuditoriaService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpContextAccessor _http;
    private readonly TimeProvider _reloj;

    public AuditoriaService(IServiceScopeFactory scopes, IHttpContextAccessor http, TimeProvider reloj)
    {
        _scopes = scopes;
        _http = http;
        _reloj = reloj;
    }

    public async Task RegistrarAsync(string accion, bool exito, Guid? usuarioId = null, string? entidad = null,
        string? entidadId = null, object? detalle = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Auditoria.Add(new RegistroAuditoria
        {
            FechaUtc = _reloj.GetUtcNow().UtcDateTime,
            UsuarioId = usuarioId,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            Detalle = detalle is null ? null : JsonSerializer.Serialize(detalle, Json),
            Exito = exito,
        });

        // CancellationToken.None: el registro se completa aunque el cliente cancele la petición.
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
