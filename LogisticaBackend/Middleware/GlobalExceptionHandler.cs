using System.Diagnostics;
using Azure;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Manejador global de excepciones que asegura respuestas conformes al estándar RFC 7807 (ProblemDetails).
/// Mitiga CWE-209 (Information Exposure through Error Messages) impidiendo fugas de trazas internas al cliente.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Excepción no controlada procesada por GlobalExceptionHandler. CorrelationId: {CorrelationId}, Ruta: {Path}",
            correlationId,
            httpContext.Request.Path
        );

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId }
        };

        switch (exception)
        {
            case ValidationException valEx:
                httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                problemDetails.Title = "Error de Validación de Negocio";
                problemDetails.Detail = "Uno o más campos enviados no cumplen las reglas de validación.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc4918#section-11.2";

                var errors = valEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                problemDetails.Extensions["errors"] = errors;
                break;

            case BadHttpRequestException badHttpEx:
                httpContext.Response.StatusCode = badHttpEx.StatusCode;
                problemDetails.Status = badHttpEx.StatusCode;
                problemDetails.Title = "Solicitud HTTP Inválida";
                problemDetails.Detail = badHttpEx.Message;
                break;

            case RequestFailedException rfEx:
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Title = "Error de Comunicación con Servicio Externo de IA";
                problemDetails.Detail = "El servicio de Azure AI Document Intelligence respondió con un error al procesar el documento.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.3";
                break;

            default:
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "Error Interno del Servidor";
                problemDetails.Detail = "Ocurrió un error inesperado al procesar la solicitud.";
                problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
                break;
        }

        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

