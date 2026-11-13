namespace LogisticaBackend.Middleware;

/// <summary>
/// Encabezados de seguridad en TODAS las respuestas de la API. La API solo devuelve JSON o
/// archivos, así que la CSP es la más estricta posible. En Development se relaja solo para /swagger.
/// </summary>
public static class EncabezadosSeguridad
{
    public const string Csp = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), fullscreen=(), " +
        "geolocation=(), gyroscope=(), magnetometer=(), microphone=(), midi=(), payment=(), " +
        "picture-in-picture=(), publickey-credentials-get=(), screen-wake-lock=(), usb=(), xr-spatial-tracking=()";

    public static IApplicationBuilder UseEncabezadosSeguridad(this IApplicationBuilder app, IHostEnvironment entorno)
    {
        return app.Use(async (http, next) =>
        {
            http.Response.OnStarting(() =>
            {
                var h = http.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Permissions-Policy"] = PermissionsPolicy;
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Cross-Origin-Resource-Policy"] = "same-origin";
                h.Remove("Server");
                h.Remove("X-Powered-By");

                var esSwaggerEnDesarrollo = entorno.IsDevelopment()
                    && http.Request.Path.StartsWithSegments("/swagger");
                if (!esSwaggerEnDesarrollo)
                {
                    h["Content-Security-Policy"] = Csp;
                }

                // Datos financieros: nada se guarda en cachés del navegador ni de proxies.
                h.CacheControl = "no-store";
                h.Pragma = "no-cache";
                return Task.CompletedTask;
            });

            await next();
        });
    }
}
