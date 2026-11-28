using System.Text.RegularExpressions;

namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Fechas escritas en la planilla: dd-MM-yy, dd/MM/yy, dd-MM-yyyy, dd/MM/yyyy o yyyy-MM-dd.
/// Los años de dos dígitos son 20yy. Nunca pasa por MontoParser: "13-06-26" no es dinero.
/// </summary>
public static partial class FechaParser
{
    [GeneratedRegex(@"^(?<d>\d{1,2})\s*[-/.]\s*(?<m>\d{1,2})\s*[-/.]\s*(?<a>\d{4}|\d{2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DiaMesAno();

    [GeneratedRegex(@"^(?<a>\d{4})-(?<m>\d{1,2})-(?<d>\d{1,2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Iso();

    /// <summary>La fecha, o null si el texto no es una fecha válida en esos formatos.</summary>
    public static DateOnly? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = texto.Trim();

        var m = Iso().Match(limpio);
        if (!m.Success) m = DiaMesAno().Match(limpio);
        if (!m.Success) return null;

        var ano = int.Parse(m.Groups["a"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (m.Groups["a"].Value.Length == 2) ano += 2000;
        var mes = int.Parse(m.Groups["m"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var dia = int.Parse(m.Groups["d"].Value, System.Globalization.CultureInfo.InvariantCulture);

        if (mes is < 1 or > 12 || dia < 1 || dia > DateTime.DaysInMonth(ano, mes)) return null;
        return new DateOnly(ano, mes, dia);
    }
}
