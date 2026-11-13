namespace LogisticaBackend.DTOs;

/// <summary>
/// Comando para guardar/aprobar una planilla (anti-overposting).
/// No incluye Id, EstadoRevision, CreadoEn, autoría, totales ni SaldoAnterior: el servidor los
/// asigna o calcula. SaldoAnterior sale del SaldoTotal de la última planilla del mismo conductor.
/// Cualquier campo extra en el JSON (id, totalGastos, saldoAnterior, ...) se ignora.
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
    List<GastoItemDto> Gastos
);

/// <summary>Renglón de gasto enviado por el formulario.</summary>
public record GastoItemDto(
    string Concepto,
    decimal Valor
);

/// <summary>Respuesta de guardar: la planilla tal como quedó con los totales del servidor.</summary>
public record PlanillaGuardadaDto(string Mensaje, int Id, PlanillaDetalleDto Data);

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
