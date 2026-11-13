namespace LogisticaBackend.DTOs;

/// <summary>
/// DTO con el detalle completo de una planilla de viaje y sus gastos asociados.
/// </summary>
public record PlanillaDetalleDto(
    int Id,
    string Placa,
    string Conductor,
    string? Origen,
    string? Destino,
    DateOnly FechaInicial,
    DateOnly FechaFinal,
    string? Manifiesto,
    string? EmpresaTransportadora,
    decimal Flete,
    decimal Anticipo,
    decimal TotalGastos,
    decimal SaldoViaje,
    decimal SaldoAnterior,
    decimal SaldoTotal,
    string EstadoRevision,
    DateTime CreadoEn,
    DateTime ActualizadoEn,
    List<GastoDetalleRespuestaDto> Gastos
);

public record GastoDetalleRespuestaDto(
    int Id,
    string Concepto,
    string ConceptoCanonico,
    decimal Valor,
    int Orden
);

/// <summary>
/// Resumen ligero para listado e historial de planillas en la interfaz.
/// </summary>
public record PlanillaResumenDto(
    int Id,
    string Placa,
    string Conductor,
    string? Origen,
    string? Destino,
    DateOnly FechaInicial,
    DateOnly FechaFinal,
    decimal TotalGastos,
    decimal SaldoViaje,
    decimal SaldoTotal,
    string EstadoRevision,
    DateTime CreadoEn
);

/// <summary>
/// Envoltorio genérico paginado para consultas de colección.
/// </summary>
public record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Elemento del catálogo canónico de conceptos de gastos.
/// </summary>
public record ConceptoCanonicoDto(
    string Clave,
    string Nombre,
    string Descripcion,
    IReadOnlyList<string> AliasComunes
);

