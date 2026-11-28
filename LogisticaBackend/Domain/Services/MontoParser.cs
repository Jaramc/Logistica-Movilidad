using System.Globalization;
using System.Text.RegularExpressions;

namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Convierte texto de montos en pesos colombianos (leído por el OCR o digitado) a decimal.
/// Las reglas son LAS MISMAS de parseCOP en LogisticaFrontend/src/features/planillas/utils/money.ts;
/// si cambias una, cambia la otra y sus casos de prueba (MontoParserTests / money.test.ts).
///
/// 1. Se quitan "$", "COP" (sin distinguir mayúsculas) y los espacios.
/// 2. Signo: negativo solo si lo que queda empieza por '-' o está entre paréntesis "( … )".
///    Un '-' en medio ("2024-11") o al final no vuelve negativo el monto.
/// 3. Separadores, sobre los dígitos, comas y puntos restantes:
///    - Con '.' y ',': el que aparece último es el decimal ("1.500.000,50", "1,500,000.50").
///    - Solo ',': decimal si hay una sola coma con 1 o 2 dígitos al final ("12,50"); si no, miles ("1,500").
///    - Solo '.': la misma regla ("12.50" decimal; "150.000" miles).
/// 4. Texto vacío o sin dígitos: 0. Resultado redondeado a 2 decimales (AwayFromZero).
/// </summary>
public static partial class MontoParser
{
    [GeneratedRegex(@"\$|COP|\s", RegexOptions.IgnoreCase)]
    private static partial Regex SimbolosYEspacios();

    [GeneratedRegex(@"[^\d.,]")]
    private static partial Regex NoNumericos();

    public static decimal ParseCop(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0m;

        var compacto = SimbolosYEspacios().Replace(texto, string.Empty);
        var esNegativo = compacto.StartsWith('-') || (compacto.StartsWith('(') && compacto.EndsWith(')'));

        var limpio = NoNumericos().Replace(compacto, string.Empty);
        if (!limpio.Any(char.IsDigit))
            return 0m;

        var tienePunto = limpio.Contains('.');
        var tieneComa = limpio.Contains(',');
        string normalizado;

        if (tienePunto && tieneComa)
        {
            normalizado = limpio.LastIndexOf('.') < limpio.LastIndexOf(',')
                ? limpio.Replace(".", "").Replace(',', '.')   // 1.500.000,50
                : limpio.Replace(",", "");                    // 1,500,000.50
        }
        else if (tieneComa)
        {
            normalizado = SepararDecimalOMiles(limpio, ',');
        }
        else if (tienePunto)
        {
            normalizado = SepararDecimalOMiles(limpio, '.');
        }
        else
        {
            normalizado = limpio;
        }

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor))
            return 0m;

        valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        return esNegativo ? -valor : valor;
    }

    private static string SepararDecimalOMiles(string limpio, char separador)
    {
        var partes = limpio.Split(separador);
        var ultima = partes[^1];
        return partes.Length == 2 && ultima.Length is 1 or 2
            ? $"{partes[0]}.{ultima}"
            : limpio.Replace(separador.ToString(), "");
    }
}
