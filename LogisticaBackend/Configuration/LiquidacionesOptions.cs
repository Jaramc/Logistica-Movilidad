namespace LogisticaBackend.Configuration;

/// <summary>Reglas de negocio configurables. Sección "Liquidaciones".</summary>
public class LiquidacionesOptions
{
    public const string SectionName = "Liquidaciones";

    /// <summary>Valor máximo aceptado en cualquier campo monetario (COP). Frena errores de digitación e inflados evidentes.</summary>
    public decimal TopeValorMonetario { get; set; } = 200_000_000m;

    /// <summary>Máximo de renglones de gasto por planilla.</summary>
    public int MaximoRenglonesGasto { get; set; } = 100;
}
