namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Pago o descuento sobre la cuenta corriente del conductor. El conductor no consigna: lo que debe
/// se le descuenta de la nómina (DescuentoNomina) y lo que la empresa le debe se le paga (PagoEmpresa).
/// Ambos los registra la administradora, el monto siempre positivo, y pueden ser parciales.
/// </summary>
public class PagoSaldo
{
    public int Id { get; set; }

    public int ConductorId { get; set; }
    public Conductor? Conductor { get; set; }

    /// <summary>Viaje que este pago está saldando, si aplica. Null para pagos generales de la cuenta.</summary>
    public int? ViajeId { get; set; }
    public Viaje? Viaje { get; set; }

    public TipoPago Tipo { get; set; }
    public decimal Valor { get; set; }
    public DateOnly Fecha { get; set; }

    /// <summary>Quincena de nómina en la que se aplicó, p. ej. "2026-12-1". Null si no aplica.</summary>
    public string? Quincena { get; set; }

    public string? Medio { get; set; }
    public string? Nota { get; set; }

    /// <summary>Usuario que registró el pago. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? RegistradoPorUsuarioId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
