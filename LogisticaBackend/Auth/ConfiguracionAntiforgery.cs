using Microsoft.AspNetCore.Antiforgery;

namespace LogisticaBackend.Auth;

/// <summary>
/// Protección CSRF con el patrón "double submit" de ASP.NET Core:
/// - cookie HttpOnly __Host-logistica-af con el token de cookie;
/// - cookie XSRF-TOKEN legible por JavaScript con el token de solicitud;
/// - el frontend reenvía XSRF-TOKEN en el encabezado X-XSRF-TOKEN.
/// El token queda ligado al usuario: el frontend debe pedir uno nuevo tras iniciar o cerrar sesión.
/// </summary>
public static class ConfiguracionAntiforgery
{
    public const string NombreCookieJs = "XSRF-TOKEN";
    public const string NombreEncabezado = "X-XSRF-TOKEN";
    public const string NombreCookieServidor = "__Host-logistica-af";

    public static IServiceCollection AddAntiforgeryLogistica(this IServiceCollection services)
    {
        services.AddAntiforgery(o =>
        {
            o.HeaderName = NombreEncabezado;
            o.FormFieldName = "__xsrf"; // no se usan formularios HTML; solo el encabezado
            o.Cookie.Name = NombreCookieServidor;
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.Path = "/";
            o.SuppressXFrameOptionsHeader = true; // lo pone el middleware de encabezados de seguridad
        });
        return services;
    }

    /// <summary>Genera los tokens y escribe la cookie XSRF-TOKEN para el usuario actual.</summary>
    public static void EmitirTokens(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(http);
        http.Response.Cookies.Append(NombreCookieJs, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // JavaScript debe leerla para copiarla al encabezado
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
    }
}
