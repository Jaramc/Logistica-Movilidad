namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Fecha del negocio en Colombia (UTC-5 fijo, sin horario de verano). No depende de la zona
/// horaria del servidor: un contenedor en UTC no debe rechazar como "futura" la fecha de hoy.
/// </summary>
public static class FechaNegocio
{
    public static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);

    public static DateOnly Hoy(TimeProvider reloj) =>
        DateOnly.FromDateTime(reloj.GetUtcNow().ToOffset(DesfaseColombia).DateTime);
}
