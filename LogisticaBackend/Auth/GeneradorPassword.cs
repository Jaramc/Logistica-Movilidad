using System.Security.Cryptography;

namespace LogisticaBackend.Auth;

/// <summary>Contraseñas temporales aleatorias (CSPRNG), sin caracteres ambiguos.</summary>
public static class GeneradorPassword
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789-_.!";

    public static string Generar(int longitud = 20)
    {
        if (longitud < 12) throw new ArgumentOutOfRangeException(nameof(longitud));
        return RandomNumberGenerator.GetString(Alfabeto, longitud);
    }
}
