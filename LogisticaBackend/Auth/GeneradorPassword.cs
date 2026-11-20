using System.Security.Cryptography;

namespace LogisticaBackend.Auth;

/// <summary>Contraseñas temporales aleatorias (CSPRNG), sin caracteres ambiguos.</summary>
public static class GeneradorPassword
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789-_.!";
    private const int MaximoIntentos = 20;

    public static string Generar(int longitud = 20)
    {
        if (longitud < 12) throw new ArgumentOutOfRangeException(nameof(longitud));
        return RandomNumberGenerator.GetString(Alfabeto, longitud);
    }

    /// <summary>
    /// Genera hasta encontrar una contraseña que pase la política (p. ej. que no contenga "lym" ni el
    /// nombre del usuario por azar). Sin esto, crear o reiniciar un usuario fallaba de vez en cuando.
    /// </summary>
    public static async Task<string> GenerarValidaAsync(Func<string, Task<bool>> cumplePolitica, int longitud = 20)
    {
        ArgumentNullException.ThrowIfNull(cumplePolitica);
        for (var intento = 0; intento < MaximoIntentos; intento++)
        {
            var password = Generar(longitud);
            if (await cumplePolitica(password)) return password;
        }
        throw new InvalidOperationException("No se pudo generar una contraseña temporal que cumpla la política.");
    }
}
