namespace LogisticaBackend.DTOs;

/// <summary>Rango obligatorio para descargar el consolidado (máximo 1 año por descarga).</summary>
public sealed class RangoFechasDescargaDto
{
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
}
