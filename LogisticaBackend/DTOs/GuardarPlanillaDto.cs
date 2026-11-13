namespace LogisticaBackend.DTOs;

/// <summary>
/// DTO de Comando estricto para Guardar/Aprobar Planilla en PostgreSQL (Anti-Overposting).
/// Excluye Id, CreadoEn, ActualizadoEn y EstadoRevision (manejados por el servidor).
/// </summary>
public record GuardarPlanillaDto(
    string Placa,
    string Conductor,
    string Origen,
    string Destino,
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
    List<GastoItemDto> Gastos
);

/// <summary>
/// Renglón individual de gasto enviado por el formulario dinámico del frontend.
/// </summary>
public record GastoItemDto(
    string Concepto,
    decimal Valor
);

