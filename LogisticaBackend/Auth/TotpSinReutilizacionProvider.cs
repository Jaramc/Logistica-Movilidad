using System.Globalization;
using System.Security.Cryptography;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Auth;

/// <summary>
/// Proveedor TOTP (RFC 6238: HMAC-SHA1, 6 dígitos, pasos de 30 s) que reemplaza al de Identity.
/// Diferencias:
/// - acepta ±1 paso (±30 s) en lugar de ±2;
/// - guarda el último paso aceptado por usuario y rechaza cualquier código de ese paso o de uno
///   anterior: un código interceptado no sirve una segunda vez.
/// La escritura del último paso usa el ConcurrencyStamp de Identity: si dos peticiones con el
/// mismo código llegan a la vez, solo una se acepta.
/// </summary>
public sealed class TotpSinReutilizacionProvider : IUserTwoFactorTokenProvider<Usuario>
{
    public const string ProveedorTokens = "[LogisticaTotp]";
    public const string NombreUltimoPaso = "UltimoPasoTotp";

    private static readonly TimeSpan Paso = TimeSpan.FromSeconds(30);
    private const int Tolerancia = 1;

    private readonly TimeProvider _reloj;

    public TotpSinReutilizacionProvider(TimeProvider reloj) => _reloj = reloj;

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<Usuario> manager, Usuario user) =>
        !string.IsNullOrWhiteSpace(await manager.GetAuthenticatorKeyAsync(user));

    /// <summary>Los códigos los genera la app del teléfono, nunca el servidor.</summary>
    public Task<string> GenerateAsync(string purpose, UserManager<Usuario> manager, Usuario user) =>
        Task.FromResult(string.Empty);

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<Usuario> manager, Usuario user)
    {
        if (token.Length != 6 || !token.All(char.IsAsciiDigit))
        {
            return false;
        }

        var claveBase32 = await manager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(claveBase32))
        {
            return false;
        }

        var clave = Base32.Decodificar(claveBase32);
        var pasoActual = PasoDe(_reloj.GetUtcNow());
        long? pasoCoincidente = null;
        for (var delta = -Tolerancia; delta <= Tolerancia; delta++)
        {
            var paso = pasoActual + delta;
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(Calcular(clave, paso)),
                    System.Text.Encoding.ASCII.GetBytes(token)))
            {
                pasoCoincidente = paso;
            }
        }
        CryptographicOperations.ZeroMemory(clave);

        if (pasoCoincidente is null)
        {
            return false;
        }

        var ultimo = await manager.GetAuthenticationTokenAsync(user, ProveedorTokens, NombreUltimoPaso);
        if (long.TryParse(ultimo, NumberStyles.None, CultureInfo.InvariantCulture, out var ultimoPaso) && pasoCoincidente <= ultimoPaso)
        {
            return false; // código ya usado (o uno anterior a uno ya usado)
        }

        var guardado = await manager.SetAuthenticationTokenAsync(
            user, ProveedorTokens, NombreUltimoPaso, pasoCoincidente.Value.ToString(CultureInfo.InvariantCulture));
        return guardado.Succeeded;
    }

    public static long PasoDe(DateTimeOffset instante) => instante.ToUnixTimeSeconds() / (long)Paso.TotalSeconds;

    /// <summary>Código de 6 dígitos para un paso dado (RFC 4226, truncamiento dinámico).</summary>
    public static string Calcular(byte[] clave, long paso)
    {
        Span<byte> contador = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(contador, paso);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(clave, contador, hash);
        var offset = hash[^1] & 0x0F;
        var binario = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binario % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}

/// <summary>Base32 (RFC 4648) sin relleno, como el que usan las apps autenticadoras.</summary>
public static class Base32
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] Decodificar(string texto)
    {
        var limpio = texto.Trim().TrimEnd('=').Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var salida = new byte[limpio.Length * 5 / 8];
        int buffer = 0, bits = 0, indice = 0;
        foreach (var c in limpio)
        {
            var valor = Alfabeto.IndexOf(c);
            if (valor < 0) throw new FormatException("Clave Base32 inválida.");
            buffer = (buffer << 5) | valor;
            bits += 5;
            if (bits >= 8)
            {
                salida[indice++] = (byte)(buffer >> (bits - 8));
                bits -= 8;
            }
        }
        return salida;
    }
}
