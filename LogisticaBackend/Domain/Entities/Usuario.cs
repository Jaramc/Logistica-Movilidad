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

    /// <summary>Vencimiento de la contraseña temporal (24 h desde que se generó).</summary>
    public DateTime? PasswordTemporalExpiraEn { get; set; }

    /// <summary>Primer uso de la contraseña temporal. Sirve una sola vez: después se rechaza.</summary>
    public DateTime? PasswordTemporalUsadaEn { get; set; }

    /// <summary>Pendiente de activación: aún no configura el 2FA.</summary>
    public bool PendienteActivacion => !TwoFactorEnabled;

    /// <summary>¿La contraseña temporal todavía se puede usar en este instante?</summary>
    public bool PasswordTemporalVigente(DateTime ahoraUtc) =>
        DebeCambiarPassword && PasswordTemporalExpiraEn is { } expira && ahoraUtc <= expira && PasswordTemporalUsadaEn is null;

    /// <summary>Asigna el ciclo de vida de una contraseña temporal recién generada.</summary>
    public void IniciarPasswordTemporal(DateTime ahoraUtc, TimeSpan vigencia)
    {
        DebeCambiarPassword = true;
        PasswordTemporalExpiraEn = ahoraUtc + vigencia;
        PasswordTemporalUsadaEn = null;
    }

    public void FinalizarPasswordTemporal()
    {
        DebeCambiarPassword = false;
        PasswordTemporalExpiraEn = null;
        PasswordTemporalUsadaEn = null;
    }
}
