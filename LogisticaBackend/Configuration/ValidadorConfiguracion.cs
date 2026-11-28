namespace LogisticaBackend.Configuration;

/// <summary>
/// Verifica al arrancar que la configuración sensible exista y no sea la de ejemplo.
/// En Production la aplicación se niega a iniciar si algo falta.
/// </summary>
public static class ValidadorConfiguracion
{
    /// <summary>Fragmentos que solo aparecen en los archivos de ejemplo.</summary>
    private static readonly string[] MarcadoresDeEjemplo =
    {
        "tu_password", "cambia_esta_password", "<tu-", "tu-recurso", "dummy"
    };

    public static string ObtenerCadenaConexion(IConfiguration configuration, IHostEnvironment entorno)
    {
        var cadena = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:DefaultConnection'. " +
                "Defínala con dotnet user-secrets (desarrollo) o con la variable de entorno " +
                "ConnectionStrings__DefaultConnection.");
        }

        if (entorno.IsProduction() && ContieneValorDeEjemplo(cadena))
        {
            throw new InvalidOperationException(
                "La cadena de conexión contiene un valor de ejemplo. Configure la contraseña real " +
                "mediante la variable de entorno ConnectionStrings__DefaultConnection.");
        }

        return cadena;
    }

    public static void ValidarAzure(AzureOcrOptions opciones, IHostEnvironment entorno)
    {
        if (!entorno.IsProduction())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(opciones.Endpoint) || string.IsNullOrWhiteSpace(opciones.ApiKey)
            || ContieneValorDeEjemplo(opciones.Endpoint) || ContieneValorDeEjemplo(opciones.ApiKey))
        {
            throw new InvalidOperationException(
                "Faltan las credenciales de Azure Document Intelligence o son de ejemplo. Configure " +
                "AzureDocumentIntelligence__Endpoint y AzureDocumentIntelligence__ApiKey.");
        }
    }

    /// <summary>
    /// En Production las llaves de Data Protection deben vivir en una carpeta persistente (volumen
    /// incluido en los backups), fuera de la base de datos. Si se pierden, se invalidan todas las
    /// sesiones y el 2FA de todos los usuarios.
    /// </summary>
    public static void ValidarDataProtection(IConfiguration configuration, IHostEnvironment entorno)
    {
        if (entorno.IsProduction() && string.IsNullOrWhiteSpace(configuration["Seguridad:RutaClavesDataProtection"]))
        {
            throw new InvalidOperationException(
                "Falta Seguridad:RutaClavesDataProtection. En producción debe apuntar a un volumen persistente incluido en los backups.");
        }
    }

    public static bool ContieneValorDeEjemplo(string valor) =>
        MarcadoresDeEjemplo.Any(m => valor.Contains(m, StringComparison.OrdinalIgnoreCase));
}
