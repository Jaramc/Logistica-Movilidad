using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Crea respuestas ProblemDetails con la fábrica del framework, para que todas pasen por
/// CustomizeProblemDetails (título en español y traceId).
/// </summary>
public static class Problemas
{
    public const string DetalleValidacion = "Uno o más campos no cumplen las reglas.";

    public static ObjectResult Crear(HttpContext http, int status, string titulo, string detalle)
    {
        var fabrica = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var pd = fabrica.CreateProblemDetails(http, status, titulo, type: null, detail: detalle);
        return Resultado(pd, status);
    }

    public static ObjectResult Validacion(
        HttpContext http,
        IReadOnlyDictionary<string, string[]> errores,
        int status = StatusCodes.Status422UnprocessableEntity,
        string titulo = "Error de validación",
        string detalle = DetalleValidacion)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (campo, mensajes) in errores)
        {
            foreach (var mensaje in mensajes)
            {
                modelState.AddModelError(campo, mensaje);
            }
        }

        var fabrica = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var pd = fabrica.CreateValidationProblemDetails(http, modelState, status, titulo, type: null, detail: detalle);
        return Resultado(pd, status);
    }

    private static ObjectResult Resultado(ProblemDetails pd, int status)
    {
        var resultado = new ObjectResult(pd) { StatusCode = status };
        resultado.ContentTypes.Add("application/problem+json");
        return resultado;
    }
}
