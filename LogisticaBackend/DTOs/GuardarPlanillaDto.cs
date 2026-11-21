namespace LogisticaBackend.DTOs;

/// <summary>
/// Comando para guardar/aprobar una planilla (anti-overposting).
/// No incluye Id, EstadoRevision, CreadoEn, autoría, totales ni SaldoAnterior: el servidor los
/// asigna o calcula. SaldoAnterior sale del SaldoTotal de la última planilla del mismo conductor.
/// Cualquier campo extra en el JSON (id, totalGastos, saldoAnterior, ...) se ignora.
/// JsonOcrCrudo es la copia de auditoría que devolvió /extraer; el servidor la valida (tamaño y
/// JSON) y si no cumple la guarda como null.
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
    List<GastoItemDto> Gastos,
    string? JsonOcrCrudo = null,
    // Valores escritos en papel, tal como los leyó el OCR (con signo: positivo = la empresa le debe
    // al conductor). Se guardan para auditoría y para marcar diferencias: nunca reemplazan el cálculo.
    decimal? TotalGastosDeclarado = null,
    decimal? SaldoViajeDeclarado = null,
    decimal? SaldoAnteriorDeclarado = null,
    decimal? SaldoNuevoDeclarado = null
);

/// <summary>Renglón de gasto enviado por el formulario.</summary>
public record GastoItemDto(
    string Concepto,
    decimal Valor
);

/// <summary>
/// Respuesta de guardar: la planilla tal como quedó con los totales del servidor y las advertencias
/// (p. ej. el saldo anterior escrito en papel difiere del calculado).
/// </summary>
public record PlanillaGuardadaDto(string Mensaje, int Id, PlanillaDetalleDto Data, List<string>? Advertencias = null);

/// <summary>Saldo anterior que el servidor le asignará a la próxima planilla del conductor.</summary>
public record SaldoAnteriorDto(string Conductor, decimal SaldoAnterior, int? PlanillaAnteriorId);

/// <summary>Filtros y paginación del historial. Tamaño máximo de página: 100.</summary>
public sealed class ConsultaPlanillasDto
{
    public const int TamanoMaximo = 100;

    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = 20;
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
    public string? Placa { get; init; }
    public string? Conductor { get; init; }
}
