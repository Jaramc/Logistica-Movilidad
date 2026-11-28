namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Transferencia de anticipo (traslado) de la empresa al conductor para un viaje. El anticipo del
/// viaje es la suma de sus traslados; puede llegar en varias.
/// </summary>
public class Traslado
{
    public int Id { get; set; }

    public int ViajeId { get; set; }
    public Viaje? Viaje { get; set; }

    public DateOnly Fecha { get; set; }
    public decimal Valor { get; set; }
    public MedioTraslado Medio { get; set; }
    public string? Nota { get; set; }

    /// <summary>Usuario que registró el traslado. Se toma de la sesión, nunca del cuerpo de la petición.</summary>
    public Guid? RegistradoPorUsuarioId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
