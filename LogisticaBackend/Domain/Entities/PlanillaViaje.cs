namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa una planilla de liquidación de viaje en transporte de carga.
/// </summary>
public class PlanillaViaje
{
    public int Id { get; set; }
    public DateOnly FechaInicial { get; set; }
    public DateOnly FechaFinal { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string Conductor { get; set; } = string.Empty;
    public string? Manifiesto { get; set; }
    public string? EmpresaTransportadora { get; set; }
    public string? Origen { get; set; }
    public string? Destino { get; set; }

    // Valores Financieros Reales (NUMERIC(14,2))
    public decimal Flete { get; set; }
    public decimal Anticipo { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal SaldoViaje { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoTotal { get; set; }

    // Control y Auditoría
    public EstadoRevision EstadoRevision { get; set; } = EstadoRevision.Aprobado;
    public string? JsonOcrCrudo { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Token de concurrencia optimista nativo de PostgreSQL (columna oculta de sistema xmin).
    /// </summary>
    public uint Version { get; set; }

    // Relación 1:N Dinámica
    public ICollection<GastoDetalle> Gastos { get; set; } = new List<GastoDetalle>();
}

