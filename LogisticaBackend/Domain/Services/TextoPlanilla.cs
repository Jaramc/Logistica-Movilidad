using System.Globalization;
using System.Text;

namespace LogisticaBackend.Domain.Services;

/// <summary>Utilidades de texto para comparar lo que lee el OCR con las etiquetas impresas del formulario.</summary>
public static class TextoPlanilla
{
    /// <summary>
    /// Mayúsculas, sin tildes y solo letras y dígitos separados por un espacio:
    /// "Nº  Manifiesto" → "NO MANIFIESTO", "ACPM / COMBUSTIBLE" → "ACPM COMBUSTIBLE".
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormKD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Colapsa espacios y saltos de línea y quita espacios y signos sueltos en los extremos.</summary>
    public static string? Limpiar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var unido = string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        unido = unido.Trim(' ', ',', ';', ':', '-', '.', '/', '|', '$');
        return unido.Length == 0 ? null : unido;
    }

    /// <summary>Cuántas letras (cualquier alfabeto) tiene el texto.</summary>
    public static int ContarLetras(string? texto) => texto?.Count(char.IsLetter) ?? 0;

    /// <summary>Distancia de edición (Levenshtein) entre dos palabras cortas.</summary>
    public static int Distancia(string a, string b)
    {
        var previa = new int[b.Length + 1];
        var actual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previa[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            actual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                actual[j] = Math.Min(Math.Min(actual[j - 1] + 1, previa[j] + 1), previa[j - 1] + costo);
            }
            (previa, actual) = (actual, previa);
        }
        return previa[b.Length];
    }
}

/// <summary>
/// Palabras que nunca son un gasto: datos de cabecera o de saldos. Las usa el lector del OCR
/// (para sacarlas de la lista de gastos) y el validador del guardado (422).
/// </summary>
public static class ListaNegraConceptos
{
    public static readonly IReadOnlySet<string> Palabras = new HashSet<string>(StringComparer.Ordinal)
    {
        "EMPRESA", "ORIGEN", "DESTINO", "FECHA", "MANIFIESTO", "PLACA", "FLETE", "ANTICIPO", "TOTAL", "SALDO", "SALDOS",
    };

    /// <summary>
    /// True si el texto empieza por una palabra de la lista ("Total gastos", "Saldo anterior",
    /// "FECHA INICIO", "N° manifiesto"). "Pago bodegaje" o "Retirada contenedor" no coinciden.
    /// </summary>
    public static bool Coincide(string? texto)
    {
        var palabras = TextoPlanilla.Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0) return false;
        // "N° MANIFIESTO" → "N MANIFIESTO" / "NO MANIFIESTO": se mira la segunda palabra.
        var primera = palabras[0] is "N" or "NO" or "NRO" && palabras.Length > 1 ? palabras[1] : palabras[0];
        return Palabras.Contains(primera);
    }
}
