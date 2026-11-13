using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Domain.Entities;

/// <summary>
/// Usuario del sistema administrativo. El correo es también el nombre de usuario.
/// No existe registro público: solo un Administrador crea usuarios.
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Un usuario inactivo no puede iniciar sesión y sus sesiones abiertas se invalidan.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Obliga a cambiar la contraseña temporal antes de usar el sistema.</summary>
    public bool DebeCambiarPassword { get; set; } = true;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public DateTime? UltimoAcceso { get; set; }
}
