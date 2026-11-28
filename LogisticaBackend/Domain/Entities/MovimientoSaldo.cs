namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Movimiento de la cuenta corriente de un conductor. Tabla de SOLO inserción (igual que
/// RegistroAuditoria): nunca se actualiza ni se borra una fila; las correcciones se hacen con un
/// movimiento Ajuste nuevo. Convención de signo: positivo = la empresa le debe al conductor.
/// </summary>
public class MovimientoSaldo
{
    public long Id { get; set; }

    public int ConductorId { get; set; }
    public Conductor? Conductor { get; set; }

    public int? ViajeId { get; set; }
    public Viaje? Viaje { get; set; }

    public int? PagoSaldoId { get; set; }
    public PagoSaldo? PagoSaldo { get; set; }

    public TipoMovimientoSaldo Tipo { get; set; }

    /// <summary>Monto con signo de este movimiento.</summary>
    public decimal Valor { get; set; }

    /// <summary>Saldo del conductor después de aplicar este movimiento.</summary>
    public decimal SaldoResultante { get; set; }

    public DateOnly FechaCorte { get; set; }

    /// <summary>Obligatoria cuando Tipo es Inicial o Ajuste; el servicio la exige antes de insertar.</summary>
    public string? Justificacion { get; set; }

    /// <summary>Usuario que generó el movimiento. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? CreadoPorUsuarioId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
