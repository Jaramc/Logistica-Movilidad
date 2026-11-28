using System.Globalization;
using System.Text;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Auth;

/// <summary>
/// Rechaza contraseñas débiles aunque tengan 12 caracteres o más:
/// - las más comunes ("contraseña123456", "Password2026!", "qwertyuiop12"…);
/// - las que contienen el nombre de la empresa o palabras prohibidas (Seguridad:PalabrasProhibidas);
/// - las que se reducen a un año, una fecha o una secuencia de números;
/// - las que contienen el correo, la parte local del correo o el nombre del usuario.
/// La comparación ignora mayúsculas, tildes, números y símbolos: "L0gística-2025!" equivale a "logistica".
/// </summary>
public class PasswordProhibidaValidator : IPasswordValidator<Usuario>
{
    /// <summary>Bases de las contraseñas más usadas (en español e inglés), sin números ni símbolos.</summary>
    private static readonly HashSet<string> Comunes = new(StringComparer.Ordinal)
    {
        "password", "passw", "pass", "contrasena", "clave", "secreto", "admin", "administrador", "administrator",
        "qwerty", "qwertyuiop", "asdfgh", "asdfghjkl", "zxcvbnm", "abc", "abcd", "abcdef", "abcdefgh", "abcdefghijkl",
        "iloveyou", "teamo", "teextrano", "bienvenido", "welcome", "letmein", "monkey", "dragon", "master", "football",
        "futbol", "colombia", "bogota", "medellin", "cali", "barranquilla", "cartagena", "nacional", "millonarios",
        "america", "junior", "jesus", "dios", "diosesamor", "amor", "princesa", "sol", "luna", "estrella", "familia",
        "usuario", "user", "test", "prueba", "cambiar", "cambiame", "temporal", "empresa", "oficina", "sistema",
        "camion", "conductor", "transporte", "carga", "gasolina", "acpm", "flota", "viaje", "planilla",
        "liquidacion", "liquidaciones", "gastos", "contabilidad", "tesoreria", "qazwsx", "trustno", "superman",
        "batman", "shadow", "sunshine", "princess", "starwars", "freedom", "whatever", "baseball", "hello", "hola",
    };

    private readonly string[] _palabrasProhibidas;

    public PasswordProhibidaValidator(IOptions<SeguridadOptions> opciones)
    {
        _palabrasProhibidas = opciones.Value.PalabrasProhibidas
            .Select(Normalizar)
            .Where(p => p.Length >= 3)
            .ToArray();
    }

    public Task<IdentityResult> ValidateAsync(UserManager<Usuario> manager, Usuario user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        // Dos lecturas: solo letras ("Password2026!" -> "password") y con leetspeak ("P4ssw0rd" -> "password").
        var bases = new[] { SoloLetras(password), Normalizar(password) }.Distinct().ToArray();
        var conDigitos = NormalizarConDigitos(password);

        if (bases.Any(b => b.Length < 6 || Comunes.Contains(b) || EsRepeticion(b)))
        {
            return Fallo("PasswordComun", "La contraseña es demasiado común o se basa en números, años o fechas. Use una frase de varias palabras.");
        }

        if (_palabrasProhibidas.Any(p => bases.Any(b => b.Contains(p, StringComparison.Ordinal))))
        {
            return Fallo("PasswordPalabraProhibida", "La contraseña no puede contener el nombre de la empresa ni palabras como \"logística\" o \"movilidad\".");
        }

        if (ContieneDatosPersonales(user, bases, conDigitos))
        {
            return Fallo("PasswordContieneDatosPersonales", "La contraseña no puede contener su correo ni su nombre.");
        }

        return Task.FromResult(IdentityResult.Success);
    }

    private static bool ContieneDatosPersonales(Usuario user, string[] bases, string conDigitos)
    {
        var fragmentos = new List<string>();
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            fragmentos.Add(user.Email);
            var local = user.Email.Split('@')[0];
            fragmentos.Add(local);
            fragmentos.AddRange(local.Split('.', '_', '-', '+'));
        }
        fragmentos.AddRange((user.NombreCompleto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var fragmento in fragmentos)
        {
            var conNumeros = NormalizarConDigitos(fragmento);
            var letras = SoloLetras(fragmento);
            if (conNumeros.Length >= 4 && conDigitos.Contains(conNumeros, StringComparison.Ordinal)) return true;
            if (letras.Length >= 4 && bases.Any(b => b.Contains(letras, StringComparison.Ordinal))) return true;
        }
        return false;
    }

    /// <summary>"aaaaaa", "abcabcabc", "jajajaja": una o dos letras, o un bloque corto repetido.</summary>
    private static bool EsRepeticion(string s)
    {
        if (s.Distinct().Count() <= 2) return true;
        for (var largo = 1; largo <= 4; largo++)
        {
            if (s.Length % largo == 0 && s.Length / largo >= 3 && string.Concat(Enumerable.Repeat(s[..largo], s.Length / largo)) == s)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Minúsculas, sin tildes, solo letras (quita dígitos, espacios y símbolos).</summary>
    public static string SoloLetras(string texto) =>
        new string(QuitarTildes(texto.ToLowerInvariant()).Where(c => c is >= 'a' and <= 'z').ToArray());

    /// <summary>Como <see cref="SoloLetras"/>, pero antes traduce leetspeak básico (0→o, 4→a, $→s…).</summary>
    public static string Normalizar(string texto)
    {
        // Primero se recortan números y símbolos de los extremos ("P4ssw0rd-2025" -> "P4ssw0rd"),
        // para que un año al final no se lea como letras.
        var recortado = new string(texto
            .SkipWhile(c => !char.IsLetter(c))
            .Reverse().SkipWhile(c => !char.IsLetter(c)).Reverse()
            .ToArray());
        var sinLeet = recortado.ToLowerInvariant()
            .Replace('0', 'o').Replace('1', 'i').Replace('3', 'e').Replace('4', 'a').Replace('5', 's').Replace('7', 't').Replace('@', 'a').Replace('$', 's');
        return new string(QuitarTildes(sinLeet).Where(c => c is >= 'a' and <= 'z').ToArray());
    }

    private static string NormalizarConDigitos(string texto) =>
        new string(QuitarTildes(texto.ToLowerInvariant()).Where(char.IsLetterOrDigit).ToArray());

    private static string QuitarTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Task<IdentityResult> Fallo(string codigo, string descripcion) =>
        Task.FromResult(IdentityResult.Failed(new IdentityError { Code = codigo, Description = descripcion }));
}
