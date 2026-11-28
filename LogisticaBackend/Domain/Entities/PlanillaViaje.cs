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

    /// <summary>Viaje al que pertenece esta planilla. Null en las planillas previas a la fase 1 (sin viaje).</summary>
    public int? ViajeId { get; set; }
    public Viaje? Viaje { get; set; }
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

    // Valores escritos en papel (con signo: positivo = la empresa le debe al conductor). Solo auditoría.
    public decimal? TotalGastosDeclarado { get; set; }
    public decimal? SaldoViajeDeclarado { get; set; }
    public decimal? SaldoAnteriorDeclarado { get; set; }
    public decimal? SaldoNuevoDeclarado { get; set; }

    /// <summary>True si algún valor escrito en papel difiere del calculado (se resalta en el Excel).</summary>
    public bool TieneDiferencias { get; set; }

    // Control y Auditoría
    public EstadoRevision EstadoRevision { get; set; } = EstadoRevision.Aprobado;
    public string? JsonOcrCrudo { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    /// <summary>Usuario que guardó la planilla. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? CreadoPorUsuarioId { get; set; }
    public Usuario? CreadoPor { get; set; }

    /// <summary>Usuario que la revisó/aprobó. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? RevisadoPorUsuarioId { get; set; }
    public Usuario? RevisadoPor { get; set; }

    /// <summary>
    /// Token de concurrencia optimista nativo de PostgreSQL (columna oculta de sistema xmin).
    /// </summary>
    public uint Version { get; set; }

    // Relación 1:N Dinámica
    public ICollection<GastoDetalle> Gastos { get; set; } = new List<GastoDetalle>();
}

