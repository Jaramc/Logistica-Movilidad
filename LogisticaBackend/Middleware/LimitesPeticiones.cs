using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Middleware;

/// <summary>Límites por minuto. Sección "LimitePeticiones" (configurable por entorno).</summary>
public class LimitePeticionesOptions
{
    public const string SectionName = "LimitePeticiones";

    /// <summary>Intentos de login (login, login-2fa, login-recuperacion) por IP.</summary>
    public int LoginPorIp { get; set; } = 5;

    /// <summary>Intentos de login por usuario (correo o usuario en verificación 2FA), sin importar la IP.</summary>
    public int LoginPorUsuario { get; set; } = 5;

    /// <summary>Análisis OCR por usuario: cada llamada a Azure cuesta dinero.</summary>
    public int OcrPorUsuario { get; set; } = 10;

    /// <summary>Peticiones totales por usuario autenticado o por IP.</summary>
    public int GlobalPorCliente { get; set; } = 120;

    public TimeSpan Ventana { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Limitador por usuario para el login. El middleware corre antes de leer el cuerpo, así que
/// el límite por correo se aplica dentro del controlador con este singleton.
/// </summary>
public sealed class LimitadorLoginPorUsuario : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limitador;

    public LimitadorLoginPorUsuario(IOptions<LimitePeticionesOptions> opciones)
    {
        var o = opciones.Value;
        _limitador = PartitionedRateLimiter.Create<string, string>(clave =>
            RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = o.LoginPorUsuario,
                Window = o.Ventana,
                QueueLimit = 0,
            }));
    }

    /// <summary>Consume un intento. Devuelve null si se permite, o los segundos de espera si no.</summary>
    public int? Intentar(string claveUsuario)
    {
        using var lease = _limitador.AttemptAcquire(claveUsuario.Trim().ToUpperInvariant());
        if (lease.IsAcquired) return null;
        return lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
            ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
            : 60;
    }

    public void Dispose() => _limitador.Dispose();
}

public static class LimitesPeticiones
{
    public const string PoliticaLogin = "login";
    public const string PoliticaOcr = "ocr";

    public static IServiceCollection AddLimitesPeticiones(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LimitePeticionesOptions>()
            .Bind(configuration.GetSection(LimitePeticionesOptions.SectionName))
            .Validate(o => o.LoginPorIp > 0 && o.LoginPorUsuario > 0 && o.OcrPorUsuario > 0 && o.GlobalPorCliente > 0,
                "LimitePeticiones: todos los límites deben ser mayores que cero.")
            .ValidateOnStart();
        services.AddSingleton<LimitadorLoginPorUsuario>();

        // Registra los servicios del middleware; la configuración real va abajo (necesita IOptions).
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<LimitePeticionesOptions>>((opciones, lim) =>
        {
            var l = lim.Value;
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(ClaveCliente(http), _ => Ventana(l.GlobalPorCliente, l.Ventana)));

            opciones.AddPolicy(PoliticaLogin, http =>
                RateLimitPartition.GetFixedWindowLimiter("ip:" + Ip(http), _ => Ventana(l.LoginPorIp, l.Ventana)));

            opciones.AddPolicy(PoliticaOcr, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClaveCliente(http), _ => Ventana(l.OcrPorUsuario, l.Ventana)));

            opciones.OnRejected = async (ctx, token) =>
            {
                var segundos = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
                    ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
                    : (int)l.Ventana.TotalSeconds;
                await EscribirLimiteExcedidoAsync(ctx.HttpContext, segundos);
            };
        });

        return services;
    }

    /// <summary>Respuesta 429 con Retry-After (el frontend lo usa para mostrar la espera).</summary>
    public static async Task EscribirLimiteExcedidoAsync(HttpContext http, int segundos)
    {
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        var servicio = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servicio.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Demasiadas solicitudes",
                Detail = $"Ha superado el límite de solicitudes. Intente de nuevo en {segundos} segundos.",
            },
        });
    }

    private static FixedWindowRateLimiterOptions Ventana(int permisos, TimeSpan ventana) => new()
    {
        PermitLimit = permisos,
        Window = ventana,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    /// <summary>Usuario autenticado si lo hay; si no, la IP (ya corregida por ForwardedHeaders).</summary>
    private static string ClaveCliente(HttpContext http)
    {
        var id = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return id is not null ? "usuario:" + id : "ip:" + Ip(http);
    }

    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
}
