using Azure;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LogisticaBackend.Services;

namespace LogisticaBackend.Middleware;

/// <summary>
/// Manejador global de excepciones (RFC 7807). Al cliente solo le llega un mensaje genérico
/// en español y un traceId para soporte: nunca ex.Message ni el stack trace (CWE-209).
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo, detalle) = Clasificar(httpContext, exception);
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalle };

        if (exception is ValidationException valEx)
        {
            // Mensajes de FluentValidation: son textos de negocio pensados para el usuario.
            problema.Extensions["errors"] = valEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception,
        });
    }

    private (int Status, string Titulo, string Detalle) Clasificar(HttpContext http, Exception exception)
    {
        switch (exception)
        {
            case OperationCanceledException when http.RequestAborted.IsCancellationRequested:
                // El cliente cerró la conexión: no es un error del servidor.
                _logger.LogInformation("Solicitud cancelada por el cliente en {Ruta}.", http.Request.Path);
                return (499, "Solicitud cancelada", "La solicitud fue cancelada.");

            case ValidationException:
                return (StatusCodes.Status422UnprocessableEntity, "Error de validación", "Uno o más campos no cumplen las reglas.");

            case ReglaDeNegocioException negocio:
                // El mensaje lo construye código interno de confianza para el usuario final (no viene del cliente).
                return (negocio.StatusCode, "Regla de negocio", negocio.Message);

            case Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
                // Otro usuario modificó el registro (xmin cambió) entre la lectura y el guardado.
                _logger.LogInformation("Conflicto de concurrencia en {Metodo} {Ruta}.", http.Request.Method, http.Request.Path);
                return (StatusCodes.Status409Conflict, "Conflicto de concurrencia",
                    "El registro fue modificado por otra persona. Recarga e intenta de nuevo.");

            case BadHttpRequestException bad:
                // Cuerpo demasiado grande, formulario mal formado, etc. El mensaje interno no se expone.
                _logger.LogWarning("Solicitud HTTP inválida ({Status}) en {Ruta}.", bad.StatusCode, http.Request.Path);
                return bad.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? (bad.StatusCode, "Archivo o solicitud demasiado grande", "La solicitud supera el tamaño máximo permitido.")
                    : (bad.StatusCode, "Solicitud inválida", "La solicitud no tiene el formato esperado.");

            case RequestFailedException rf:
                // Se registra el código de Azure (sin la excepción completa, que trae el cuerpo de la respuesta).
                _logger.LogError("Azure Document Intelligence falló. Código: {CodigoAzure}. Estado HTTP: {EstadoAzure}.",
                    rf.ErrorCode, rf.Status);
                return (StatusCodes.Status502BadGateway, "Servicio externo no disponible",
                    "No fue posible procesar el documento en este momento. Intente de nuevo más tarde.");

            case Npgsql.NpgsqlException or TimeoutException
                or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException:
                _logger.LogError(exception, "Fallo de base de datos en {Ruta}.", http.Request.Path);
                return (StatusCodes.Status503ServiceUnavailable, "Servicio no disponible",
                    "La base de datos no está disponible en este momento. Intente de nuevo más tarde.");

            default:
                _logger.LogError(exception, "Excepción no controlada en {Metodo} {Ruta}.", http.Request.Method, http.Request.Path);
                return (StatusCodes.Status500InternalServerError, "Error interno",
                    "Ocurrió un error inesperado. Si persiste, comuníquese con soporte e indique el código de seguimiento.");
        }
    }
}
