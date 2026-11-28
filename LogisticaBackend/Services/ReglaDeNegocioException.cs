namespace LogisticaBackend.Services;

/// <summary>
/// Error de regla de negocio (no de validación de campos) con un mensaje seguro para mostrar al
/// usuario y el código HTTP que el GlobalExceptionHandler debe devolver (409, 422, ...).
/// </summary>
public sealed class ReglaDeNegocioException : Exception
{
    public int StatusCode { get; }

    public ReglaDeNegocioException(int statusCode, string mensaje) : base(mensaje) => StatusCode = statusCode;
}
