namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Conductor identificado por su cédula, con una cuenta corriente propia (ver MovimientoSaldo).
/// Lo crea la administradora desde el panel; no hay registro público ni autoservicio.
/// </summary>
public class Conductor
{
    public int Id { get; set; }

    /// <summary>Código corto generado por el servidor (C01, C02, …), nunca editable.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string Cedula { get; set; } = string.Empty;

    /// <summary>Cuenta de Microsoft del conductor, para la app móvil de la fase 2. Aún no se usa.</summary>
    public string? CuentaMicrosoft { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
