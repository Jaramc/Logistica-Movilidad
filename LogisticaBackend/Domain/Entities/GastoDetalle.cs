namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Renglón individual dinámico de gasto perteneciente a una planilla de viaje.
/// </summary>
public class GastoDetalle
{
    public int Id { get; set; }
    public int PlanillaViajeId { get; set; }

    /// <summary>
    /// Concepto textual extraído del comprobante o digitado por el usuario.
    /// </summary>
    public string Concepto { get; set; } = string.Empty;

    /// <summary>
    /// Clasificación normalizada en uno de los 16 rubros canónicos del Excel o 'OTROS'.
    /// </summary>
    public string ConceptoCanonico { get; set; } = "OTROS";

    /// <summary>
    /// Importe económico en COP con precisión de dos decimales.
    /// </summary>
    public decimal Valor { get; set; }

    /// <summary>
    /// Orden de aparición en el documento físico.
    /// </summary>
    public int Orden { get; set; }

    // Propiedad de navegación
    public PlanillaViaje? PlanillaViaje { get; set; }
}

