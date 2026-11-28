using System.Text;
using System.Text.Json;

namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Copia de auditoría de los campos que leyó el OCR (nombre, content, confidence), guardada en jsonb.
/// La envía el cliente, así que el servidor solo la acepta si cabe en el tope y es JSON válido.
/// Si no, se guarda null y se registra un warning: la planilla ya fue aprobada por el revisor y
/// este dato es informativo, no debe impedir el guardado.
/// </summary>
public static class JsonOcrCrudo
{
    public const int TamanoMaximoBytes = 256 * 1024;

    public enum Resultado { Vacio, Aceptado, DemasiadoGrande, Invalido }

    public static (string? Json, Resultado Resultado) Sanear(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, Resultado.Vacio);

        if (Encoding.UTF8.GetByteCount(json) > TamanoMaximoBytes)
            return (null, Resultado.DemasiadoGrande);

        // jsonb de PostgreSQL rechaza \u0000 aunque sea JSON válido: guardarlo daría un 500.
        if (json.Contains("\\u0000", StringComparison.OrdinalIgnoreCase))
            return (null, Resultado.Invalido);

        try
        {
            using var _ = JsonDocument.Parse(json);
            return (json, Resultado.Aceptado);
        }
        catch (JsonException)
        {
            return (null, Resultado.Invalido);
        }
    }
}
