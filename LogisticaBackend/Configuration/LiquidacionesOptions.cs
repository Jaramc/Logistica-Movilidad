namespace LogisticaBackend.Configuration;

/// <summary>Reglas de negocio configurables. Sección "Liquidaciones".</summary>
public class LiquidacionesOptions
{
    public const string SectionName = "Liquidaciones";

    /// <summary>
    /// Máximo por campo de gasto, flete y anticipo (COP). Ninguno puede ser negativo.
    /// Frena errores de digitación e inflados evidentes.
    /// </summary>
    public decimal TopeValorMonetario { get; set; } = 20_000_000m;

    /// <summary>
    /// Máximo, en valor absoluto, para SaldoViaje, SaldoAnterior y SaldoTotal (COP).
    /// Los saldos SÍ pueden ser negativos (el conductor debe dinero o la empresa le debe a él).
    /// </summary>
    public decimal TopeSaldo { get; set; } = 100_000_000m;

    /// <summary>Máximo de renglones de gasto por planilla (la planilla impresa tiene menos de 30).</summary>
    public int MaximoRenglonesGasto { get; set; } = 60;
}
