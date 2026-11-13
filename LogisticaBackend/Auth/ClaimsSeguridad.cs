namespace LogisticaBackend.Auth;

/// <summary>Claims propios que viajan en la cookie de sesión.</summary>
public static class ClaimsSeguridad
{
    /// <summary>Método de autenticación (RFC 8176). Se emite como "mfa" solo tras el segundo factor.</summary>
    public const string Amr = "amr";
    public const string AmrMfa = "mfa";

    /// <summary>"true" mientras el usuario deba cambiar la contraseña temporal.</summary>
    public const string DebeCambiarPassword = "debe_cambiar_password";

    public const string NombreCompleto = "nombre_completo";

    /// <summary>Clave en AuthenticationProperties.Items con el instante del login (expiración absoluta).</summary>
    public const string ItemInicioSesion = "inicio_sesion_utc";
}
