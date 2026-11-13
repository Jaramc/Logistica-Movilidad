namespace LogisticaBackend.DTOs;

/// <summary>
/// Envoltorio estándar de respuesta OCR para el frontend.
/// </summary>
public record PlanillaOcrResponseDto(
    string Mensaje,
    PlanillaOcrDataDto Data
);

/// <summary>
/// Datos financieros y de ruta extraídos por Azure Document Intelligence.
/// Todos los importes monetarios utilizan 'decimal' (precisión contable exacta).
/// </summary>
public record PlanillaOcrDataDto(
    string? Placa,
    string? Origen,
    string? Destino,
    decimal Flete,
    decimal Anticipo,
    decimal TotalGastos,
    decimal SaldoViaje,
    List<GastoDetalleOcrDto> DetalleGastos,
    string? Conductor = null,
    DateOnly? FechaInicial = null,
    DateOnly? FechaFinal = null,
    string? Manifiesto = null,
    string? EmpresaTransportadora = null,
    List<string>? Advertencias = null
);

/// <summary>
/// Renglón individual extraído de la tabla de gastos de la planilla.
/// </summary>
public record GastoDetalleOcrDto(
    string Concepto,
    decimal Valor
);
