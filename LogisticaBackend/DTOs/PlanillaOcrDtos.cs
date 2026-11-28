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
    List<string>? Advertencias = null,
    /// <summary>Promedio de Confidence (0–1) de los campos que el modelo encontró.</summary>
    float? ConfianzaPromedio = null,
    /// <summary>Campos esperados que no vinieron o vinieron vacíos (p. ej. "Flete"), para resaltarlos en la UI.</summary>
    List<string>? CamposNoEncontrados = null,
    /// <summary>JSON con los campos extraídos: [{ nombre, content, confidence }]. Se reenvía al guardar.</summary>
    string? JsonOcrCrudo = null,
    /// <summary>Total de gastos escrito en la planilla (no el calculado).</summary>
    decimal? TotalGastosDeclarado = null,
    /// <summary>Saldos escritos en la planilla, con signo: positivo = la empresa le debe al conductor.</summary>
    SaldoDeclaradoDto? SaldoViajeDeclarado = null,
    SaldoDeclaradoDto? SaldoAnteriorDeclarado = null,
    SaldoDeclaradoDto? SaldoNuevoDeclarado = null
);

/// <summary>Saldo escrito en la planilla: valor con signo y a favor de quién ("Conductor" o "Empresa").</summary>
public record SaldoDeclaradoDto(decimal Valor, string? AFavorDe);

/// <summary>
/// Renglón de gasto de la planilla. Concepto = la etiqueta impresa ("PEAJES") o, en filas libres,
/// lo escrito a mano. Descripcion = la nota escrita a mano. CategoriaPropuesta = una de las 16.
/// </summary>
public record GastoDetalleOcrDto(
    string Concepto,
    decimal Valor,
    float? Confianza = null,
    string Etiqueta = "",
    string? Descripcion = null,
    string CategoriaPropuesta = "OTROS"
);
