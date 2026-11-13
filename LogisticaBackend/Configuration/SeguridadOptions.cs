namespace LogisticaBackend.Configuration;

/// <summary>Parámetros de sesión y autenticación. Sección "Seguridad".</summary>
public class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    /// <summary>Cierre por inactividad (expiración deslizante de la cookie).</summary>
    public TimeSpan InactividadSesion { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Duración máxima de una sesión desde el login, aunque haya actividad.</summary>
    public TimeSpan DuracionMaximaSesion { get; set; } = TimeSpan.FromHours(8);

    /// <summary>Cada cuánto se revalida el security stamp contra la base de datos (máximo 5 minutos).</summary>
    public TimeSpan IntervaloValidacionSesion { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Nombre que muestra la app autenticadora junto a la cuenta.</summary>
    public string Emisor2fa { get; set; } = "Logistica LM";

    /// <summary>Carpeta persistente de las claves de Data Protection (cifran las cookies).
    /// En Docker debe ser un volumen; si se pierde, todas las sesiones se cierran.</summary>
    public string? RutaClavesDataProtection { get; set; }
}
