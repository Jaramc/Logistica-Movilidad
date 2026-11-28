using System.Text.RegularExpressions;

namespace LogisticaBackend.Domain.Services;

/// <summary>Placas de vehículos de carga colombianos: tres letras y tres números (p. ej. WCN410).</summary>
public static partial class Placa
{
    [GeneratedRegex("^[A-Z]{3}[0-9]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Formato();

    /// <summary>Mayúsculas, sin espacios ni guiones ("wcn-410" → "WCN410").</summary>
    public static string Normalizar(string? placa) =>
        new string((placa ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    public static bool EsValida(string? placa) => Formato().IsMatch(Normalizar(placa));
}
