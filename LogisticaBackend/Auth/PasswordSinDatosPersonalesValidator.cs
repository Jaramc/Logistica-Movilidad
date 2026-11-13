using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Auth;

/// <summary>Rechaza contraseñas que contienen el correo o el nombre del usuario.</summary>
public class PasswordSinDatosPersonalesValidator : IPasswordValidator<Usuario>
{
    public Task<IdentityResult> ValidateAsync(UserManager<Usuario> manager, Usuario user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        var fragmentos = new List<string>();
        var local = user.Email?.Split('@')[0];
        if (!string.IsNullOrWhiteSpace(local)) fragmentos.Add(local);
        fragmentos.AddRange((user.NombreCompleto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var contieneDatos = fragmentos
            .Where(f => f.Length >= 4)
            .Any(f => password.Contains(f, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(contieneDatos
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordContieneDatosPersonales",
                Description = "La contraseña no puede contener su correo ni su nombre."
            })
            : IdentityResult.Success);
    }
}
