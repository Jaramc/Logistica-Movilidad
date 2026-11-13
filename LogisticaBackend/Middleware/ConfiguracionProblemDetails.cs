using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Todas las respuestas de error usan ProblemDetails en español con un traceId para soporte.
/// Incluye los errores sin cuerpo (404, 405, 415) y los de enlace de modelo (JSON mal formado).
/// </summary>
public static class ConfiguracionProblemDetails
{
    private static readonly Dictionary<int, string> Titulos = new()
    {
        [400] = "Solicitud inválida",
        [401] = "No autenticado",
        [403] = "Acceso denegado",
        [404] = "No encontrado",
        [405] = "Método no permitido",
        [409] = "Conflicto",
        [413] = "Solicitud demasiado grande",
        [415] = "Tipo de contenido no admitido",
        [422] = "Error de validación",
        [429] = "Demasiadas solicitudes",
        [500] = "Error interno",
        [502] = "Servicio externo no disponible",
        [503] = "Servicio no disponible",
    };

    public static IServiceCollection AddProblemDetailsLogistica(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            var pd = ctx.ProblemDetails;
            var status = pd.Status ?? ctx.HttpContext.Response.StatusCode;

            // Reemplaza los títulos en inglés del framework ("Not Found", "One or more validation errors occurred.").
            if (Titulos.TryGetValue(status, out var titulo) && !EsTituloPropio(pd.Title))
            {
                pd.Title = titulo;
            }
            pd.Type = null; // no se enlaza a documentación externa
            pd.Instance = null; // la ruta no aporta y puede incluir identificadores
            pd.Extensions.Remove("exception");
            pd.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier;
        });

        // Errores de enlace del modelo ([ApiController]): no se reenvían los mensajes de System.Text.Json,
        // que describen la estructura interna. Solo se indica qué campo tiene formato inválido.
        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx =>
        {
            var errores = ctx.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .Select(e => string.IsNullOrEmpty(e.Key) ? "cuerpo" : e.Key.TrimStart('$', '.'))
                .Distinct()
                .ToDictionary(campo => campo, _ => new[] { "El valor no tiene el formato esperado." });

            return Problemas.Validacion(ctx.HttpContext, errores, StatusCodes.Status400BadRequest,
                "Solicitud inválida", "Uno o más campos no tienen el formato esperado.");
        });

        return services;
    }

    /// <summary>Un título propio (en español) se respeta; los títulos estándar del framework se traducen.</summary>
    private static bool EsTituloPropio(string? titulo) =>
        !string.IsNullOrEmpty(titulo) && !TitulosFramework.Contains(titulo);

    private static readonly HashSet<string> TitulosFramework = new(StringComparer.OrdinalIgnoreCase)
    {
        "One or more validation errors occurred.", "Bad Request", "Unauthorized", "Forbidden", "Not Found",
        "Method Not Allowed", "Conflict", "Payload Too Large", "Unsupported Media Type", "Unprocessable Entity",
        "Too Many Requests", "An error occurred while processing your request.", "Internal Server Error",
        "Bad Gateway", "Service Unavailable",
    };
}
