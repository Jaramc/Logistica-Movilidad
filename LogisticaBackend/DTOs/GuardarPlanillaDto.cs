namespace LogisticaBackend.DTOs;

/// <summary>
/// Comando para guardar/aprobar una planilla (anti-overposting).
/// No incluye Id, EstadoRevision, CreadoEn, autoría, totales ni SaldoAnterior: el servidor los
/// asigna o calcula. El conductor y la placa de la entidad salen del viaje (ViajeId), no del
/// cuerpo de la petición; Placa aquí es solo lo que leyó el OCR, para advertir si no coincide con
/// la del viaje. SaldoAnterior sale del saldo actual de la cuenta corriente del conductor (ver
/// CuentaConductorService), no de una cadena de planillas por nombre.
/// Cualquier campo extra en el JSON (id, totalGastos, saldoAnterior, ...) se ignora.
/// JsonOcrCrudo es la copia de auditoría que devolvió /extraer; el servidor la valida (tamaño y
/// JSON) y si no cumple la guarda como null.
/// </summary>
public record GuardarPlanillaDto(
    int ViajeId,
    string Origen,
    string Destino,
    DateOnly FechaInicial,
    DateOnly FechaFinal,
    decimal Flete,
    decimal Anticipo,
    List<GastoItemDto> Gastos,
    string? Placa = null,
    string? Manifiesto = null,
    string? EmpresaTransportadora = null,
    string? JsonOcrCrudo = null,
    // Valores escritos en papel, tal como los leyó el OCR (con signo: positivo = la empresa le debe
    // al conductor). Se guardan para auditoría y para marcar diferencias: nunca reemplazan el cálculo.
    decimal? TotalGastosDeclarado = null,
    decimal? SaldoViajeDeclarado = null,
    decimal? SaldoAnteriorDeclarado = null,
    decimal? SaldoNuevoDeclarado = null
);

/// <summary>
/// Renglón de gasto enviado por el formulario. Etiqueta = renglón impreso ("PEAJES"), Descripcion =
/// nota escrita a mano, Categoria = una de las 16 elegida en la UI (si falta, se deduce del concepto).
/// </summary>
public record GastoItemDto(
    string Concepto,
    decimal Valor,
    string? Etiqueta = null,
    string? Descripcion = null,
    string? Categoria = null
);

/// <summary>
/// Respuesta de guardar: la planilla tal como quedó con los totales del servidor y las advertencias
/// (p. ej. el saldo anterior escrito en papel difiere del calculado).
/// </summary>
public record PlanillaGuardadaDto(string Mensaje, int Id, PlanillaDetalleDto Data, List<string>? Advertencias = null);

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
