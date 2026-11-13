using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Auth;

/// <summary>
/// Identity consulta esta clase antes de cada inicio de sesión (con RequireConfirmedAccount = true).
/// Un usuario desactivado no puede iniciar sesión ni completar el segundo factor.
/// </summary>
public class UsuarioActivoConfirmation : IUserConfirmation<Usuario>
{
    public Task<bool> IsConfirmedAsync(UserManager<Usuario> manager, Usuario user) => Task.FromResult(user.Activo);
}
