namespace LogisticaBackend.DTOs;

public sealed record RegistroAuditoriaDto(
    long Id,
    DateTime FechaUtc,
    Guid? UsuarioId,
    string? UsuarioEmail,
    string Accion,
    string? Entidad,
    string? EntidadId,
    string? Ip,
    string? Detalle,
    bool Exito);

public sealed class ConsultaAuditoriaDto
{
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = 50;
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
    public string? Accion { get; init; }
    public Guid? UsuarioId { get; init; }
}
