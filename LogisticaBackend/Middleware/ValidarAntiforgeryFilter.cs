using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Exige el token antiforgery (encabezado X-XSRF-TOKEN) en todo POST, PUT, PATCH y DELETE,
/// incluido el login. Responde 400 con ProblemDetails en español.
/// </summary>
public sealed class ValidarAntiforgeryFilter : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> MetodosSeguros = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Trace
    };

    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<ValidarAntiforgeryFilter> _logger;

    public ValidarAntiforgeryFilter(IAntiforgery antiforgery, ILogger<ValidarAntiforgeryFilter> logger)
    {
        _antiforgery = antiforgery;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (MetodosSeguros.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            _logger.LogWarning("Token antiforgery ausente o inválido en {Metodo} {Ruta}.",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            context.Result = Problemas.Crear(context.HttpContext, StatusCodes.Status400BadRequest,
                "Solicitud rechazada", "Falta el token de seguridad o expiró. Recargue la página e intente de nuevo.");
        }
    }
}
