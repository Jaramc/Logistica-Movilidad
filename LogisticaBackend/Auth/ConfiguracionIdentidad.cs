using System.Globalization;
using System.Security.Claims;
using LogisticaBackend.Configuration;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Auth;

public static class ConfiguracionIdentidad
{
    public const string NombreCookieSesion = "__Host-logistica";
    public const string NombreCookie2fa = "__Host-logistica-2fa";
    public const string NombreProveedorTotp = "LogisticaTotp";

    public static IServiceCollection AddIdentidadLogistica(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SeguridadOptions>()
            .Bind(configuration.GetSection(SeguridadOptions.SectionName))
            .Validate(s => s.IntervaloValidacionSesion <= TimeSpan.FromMinutes(5),
                "Seguridad:IntervaloValidacionSesion no puede superar 5 minutos.")
            .Validate(s => s.InactividadSesion > TimeSpan.Zero && s.DuracionMaximaSesion >= s.InactividadSesion,
                "Seguridad: la duración máxima de sesión debe ser mayor o igual a la inactividad.")
            .ValidateOnStart();
        var rutaClaves = configuration[$"{SeguridadOptions.SectionName}:{nameof(SeguridadOptions.RutaClavesDataProtection)}"];

        services.AddIdentity<Usuario, IdentityRole<Guid>>(o =>
            {
                // Longitud mínima sin reglas de composición (NIST SP 800-63B).
                o.Password.RequiredLength = 12;
                o.Password.RequiredUniqueChars = 5;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;

                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;

                o.User.RequireUniqueEmail = true;

                // Activa IUserConfirmation: un usuario inactivo no puede iniciar sesión.
                o.SignIn.RequireConfirmedAccount = true;
                o.SignIn.RequireConfirmedEmail = false;
                o.SignIn.RequireConfirmedPhoneNumber = false;

                // TOTP propio: ±30 s y sin reutilización del mismo código.
                o.Tokens.AuthenticatorTokenProvider = NombreProveedorTotp;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserStore<UsuarioStore>()
            .AddClaimsPrincipalFactory<UsuarioClaimsPrincipalFactory>()
            .AddErrorDescriber<IdentityErrorDescriberEs>()
            .AddPasswordValidator<PasswordProhibidaValidator>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<TotpSinReutilizacionProvider>(NombreProveedorTotp);

        services.AddScoped<IUserConfirmation<Usuario>, UsuarioActivoConfirmation>();

        var dataProtection = services.AddDataProtection().SetApplicationName("LogisticaBackend");
        if (!string.IsNullOrWhiteSpace(rutaClaves))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
        }

        services.AddOptions<SecurityStampValidatorOptions>().Configure<IOptions<SeguridadOptions>>((o, seg) =>
        {
            o.ValidationInterval = seg.Value.IntervaloValidacionSesion;
            // Al reconstruir la sesión tras validar el stamp, se conserva el claim de MFA.
            o.OnRefreshingPrincipal = ctx =>
            {
                var amr = ctx.CurrentPrincipal?.FindFirst(ClaimsSeguridad.Amr);
                if (amr is not null && ctx.NewPrincipal?.Identity is ClaimsIdentity nueva
                    && !nueva.HasClaim(ClaimsSeguridad.Amr, amr.Value))
                {
                    nueva.AddClaim(new Claim(ClaimsSeguridad.Amr, amr.Value));
                }
                return Task.CompletedTask;
            };
        });

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<SeguridadOptions>>((o, seg) =>
        {
            var seguridad = seg.Value;
            ConfigurarCookieSegura(o.Cookie, NombreCookieSesion);
            o.ExpireTimeSpan = seguridad.InactividadSesion;
            o.SlidingExpiration = true;

            o.Events.OnRedirectToLogin = ctx => EscribirProblemaAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                "No autenticado", "Debe iniciar sesión.");
            o.Events.OnRedirectToAccessDenied = ctx => EscribirProblemaAsync(ctx.HttpContext, StatusCodes.Status403Forbidden,
                "Acceso denegado", "No tiene permisos para realizar esta acción.");
            o.Events.OnRedirectToLogout = ctx => { ctx.Response.StatusCode = StatusCodes.Status204NoContent; return Task.CompletedTask; };
            o.Events.OnRedirectToReturnUrl = ctx => { ctx.Response.StatusCode = StatusCodes.Status204NoContent; return Task.CompletedTask; };

            o.Events.OnSigningIn = ctx =>
            {
                ctx.Properties.IsPersistent = false; // cookie de sesión: se borra al cerrar el navegador
                var ahora = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
                ctx.Properties.Items.TryAdd(ClaimsSeguridad.ItemInicioSesion, ahora.ToString("O", CultureInfo.InvariantCulture));
                return Task.CompletedTask;
            };

            o.Events.OnValidatePrincipal = async ctx =>
            {
                if (SesionExcedeDuracionMaxima(ctx, seguridad.DuracionMaximaSesion))
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }
                await SecurityStampValidator.ValidatePrincipalAsync(ctx);
            };
        });

        // Cookie intermedia entre la contraseña y el código 2FA: corta y con los mismos atributos.
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, o =>
        {
            ConfigurarCookieSegura(o.Cookie, NombreCookie2fa);
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, o =>
            ConfigurarCookieSegura(o.Cookie, "__Host-logistica-recordar"));
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ExternalScheme, o =>
            ConfigurarCookieSegura(o.Cookie, "__Host-logistica-externo"));

        services.AddAuthorization(Politicas.Registrar);
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    private static void ConfigurarCookieSegura(CookieBuilder cookie, string nombre)
    {
        cookie.Name = nombre;
        cookie.HttpOnly = true;
        cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.SameSite = SameSiteMode.Strict;
        cookie.Path = "/"; // requisito del prefijo __Host-
        cookie.Domain = null;
        cookie.IsEssential = true;
    }

    private static bool SesionExcedeDuracionMaxima(CookieValidatePrincipalContext ctx, TimeSpan maximo)
    {
        if (!ctx.Properties.Items.TryGetValue(ClaimsSeguridad.ItemInicioSesion, out var valor)
            || !DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var inicio))
        {
            return true; // sin marca de inicio no se acepta la sesión
        }
        var ahora = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        return ahora - inicio > maximo;
    }

    private static async Task EscribirProblemaAsync(HttpContext http, int status, string titulo, string detalle)
    {
        http.Response.StatusCode = status;
        var servicio = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await servicio.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = detalle }
        });
    }
}
