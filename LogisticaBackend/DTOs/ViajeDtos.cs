namespace LogisticaBackend.DTOs;

public record AbrirViajeDto(int ConductorId, int VehiculoId, string Origen, string Destino, string Cliente, string? Manifiesto, decimal Flete, DateOnly FechaInicio);

/// <summary>Medio: "Transferencia", "Efectivo" o "Giro".</summary>
public record RegistrarTrasladoDto(DateOnly Fecha, decimal Valor, string Medio, string? Nota);

public record ReabrirViajeDto(string? Motivo);

public record TrasladoDto(int Id, DateOnly Fecha, decimal Valor, string Medio, string? Nota, DateTime CreadoEn);

public record ViajeResumenDto(
    int Id, int ConductorId, string ConductorCodigo, string ConductorNombre, int VehiculoId, string Placa,
    string Origen, string Destino, string Cliente, decimal Flete, DateOnly FechaInicio, DateOnly? FechaFin,
    string Estado, decimal? SaldoViaje, DateTime? AprobadoEn
);

public record ViajeDetalleDto(
    int Id, int ConductorId, string ConductorCodigo, string ConductorNombre, int VehiculoId, string Placa,
    string Origen, string Destino, string Cliente, string? Manifiesto, decimal Flete,
    DateOnly FechaInicio, DateOnly? FechaFin, string Estado, decimal? SaldoViaje, decimal? Pendiente,
    decimal AnticipoTraslados, int? PlanillaId, List<TrasladoDto> Traslados
);

public record AprobarViajeRespuestaDto(ViajeDetalleDto Viaje, List<string>? Advertencias);

/// <summary>Filtros y paginación del listado de viajes. Tamaño máximo de página: 100.</summary>
public sealed class ConsultaViajesDto
{
    public const int TamanoMaximo = 100;

    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = 20;
    public string? Estado { get; init; }
    public int? ConductorId { get; init; }
    public string? Placa { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
}
