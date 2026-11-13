using System.Security.Claims;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Auth;

/// <summary>Agrega a la sesión el nombre y la marca de contraseña temporal. Se recalcula al validar el security stamp.</summary>
public class UsuarioClaimsPrincipalFactory : UserClaimsPrincipalFactory<Usuario, IdentityRole<Guid>>
{
    public UsuarioClaimsPrincipalFactory(
        UserManager<Usuario> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario user)
    {
        var identidad = await base.GenerateClaimsAsync(user);
        identidad.AddClaim(new Claim(ClaimsSeguridad.NombreCompleto, user.NombreCompleto));
        if (user.DebeCambiarPassword)
        {
            identidad.AddClaim(new Claim(ClaimsSeguridad.DebeCambiarPassword, "true"));
        }
        return identidad;
    }
}
