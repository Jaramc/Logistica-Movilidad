namespace LogisticaBackend.Services;

public record ValidacionArchivoResultado(
    bool EsValido,
    string? MensajeError,
    string? TipoDetectado
);

/// <summary>
/// Validador estricto de archivos en backend mediante inspección de firmas binarias (Magic Bytes).
/// Mitiga CWE-434 (Unrestricted File Upload) y ataques de suplantación de MIME types.
/// </summary>
public static class FileValidationService
{
    public const long TamanoMaximoBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, byte[][]> FirmasValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } }, // %PDF
        { "image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { "image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }
    };

    private static readonly Dictionary<string, string[]> ExtensionesPorMime = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/pdf", new[] { ".pdf" } },
        { "image/png", new[] { ".png" } },
        { "image/jpeg", new[] { ".jpg", ".jpeg" } }
    };

    public static async Task<ValidacionArchivoResultado> ValidarArchivoAsync(IFormFile? archivo, CancellationToken cancellationToken = default)
    {
        if (archivo == null || archivo.Length == 0)
        {
            return new ValidacionArchivoResultado(false, "No se suministró ningún archivo o el archivo está vacío.", null);
        }

        if (archivo.Length > TamanoMaximoBytes)
        {
            return new ValidacionArchivoResultado(false, $"El archivo excede el tamaño máximo permitido de 10 MB ({archivo.Length / (1024 * 1024.0):F1} MB recibidos).", null);
        }

        var contentType = archivo.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!FirmasValidas.ContainsKey(contentType))
        {
            return new ValidacionArchivoResultado(false, "Tipo MIME no permitido. Únicamente se admiten archivos PDF, JPEG o PNG.", null);
        }

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !ExtensionesPorMime.TryGetValue(contentType, out var extensionesValidas) || !extensionesValidas.Contains(extension))
        {
            return new ValidacionArchivoResultado(false, "La extensión del archivo no coincide con el tipo MIME declarado.", null);
        }

        // Inspección de cabecera binaria (Magic Bytes)
        try
        {
            using var stream = archivo.OpenReadStream();
            var maxFirma = FirmasValidas[contentType].Max(f => f.Length);
            var buffer = new byte[maxFirma];
            var leidos = await stream.ReadAsync(buffer.AsMemory(0, maxFirma), cancellationToken);

            if (leidos < maxFirma)
            {
                return new ValidacionArchivoResultado(false, "El archivo está truncado o dañado.", null);
            }

            bool coincideFirma = FirmasValidas[contentType].Any(firma =>
                buffer.Take(firma.Length).SequenceEqual(firma)
            );

            if (!coincideFirma)
            {
                return new ValidacionArchivoResultado(false, "El contenido binario no coincide con la firma auténtica de un archivo PDF, JPEG o PNG.", null);
            }

            return new ValidacionArchivoResultado(true, null, contentType);
        }
        catch
        {
            return new ValidacionArchivoResultado(false, "Error de E/S al leer los bytes del archivo.", null);
        }
    }
}

