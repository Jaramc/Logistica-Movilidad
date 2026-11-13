using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Valida con FluentValidation todo argumento de acción que tenga un IValidator registrado.
/// Responde 422 con ProblemDetails en español y el detalle por campo.
/// </summary>
public sealed class ValidacionFluentFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argumento in context.ActionArguments.Values)
        {
            if (argumento is null) continue;

            var tipoValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());
            if (context.HttpContext.RequestServices.GetService(tipoValidador) is not IValidator validador) continue;

            var resultado = await validador.ValidateAsync(
                new ValidationContext<object>(argumento), context.HttpContext.RequestAborted);
            if (resultado.IsValid) continue;

            var errores = resultado.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            context.Result = new ObjectResult(new ValidationProblemDetails(errores)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Error de validación",
                Detail = "Uno o más campos no cumplen las reglas.",
            })
            { StatusCode = StatusCodes.Status422UnprocessableEntity };
            return;
        }

        await next();
    }
}
