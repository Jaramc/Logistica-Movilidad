using System.Text;
using FluentAssertions;
using LogisticaBackend.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LogisticaBackend.Tests;

public class FileValidationServiceTests
{
    private static IFormFile CrearFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public async Task ValidarArchivoAsync_PdfValido_PasaValidacion()
    {
        var magicPdf = Encoding.ASCII.GetBytes("%PDF-1.4 header dummy content");
        var file = CrearFormFile(magicPdf, "planilla.pdf", "application/pdf");

        var resultado = await FileValidationService.ValidarArchivoAsync(file);

        resultado.EsValido.Should().BeTrue();
        resultado.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task ValidarArchivoAsync_PngValido_PasaValidacion()
    {
        var magicPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00 };
        var file = CrearFormFile(magicPng, "foto_planilla.png", "image/png");

        var resultado = await FileValidationService.ValidarArchivoAsync(file);

        resultado.EsValido.Should().BeTrue();
    }

    [Fact]
    public async Task ValidarArchivoAsync_JpegValido_PasaValidacion()
    {
        var magicJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var file = CrearFormFile(magicJpeg, "recibo.jpg", "image/jpeg");

        var resultado = await FileValidationService.ValidarArchivoAsync(file);

        resultado.EsValido.Should().BeTrue();
    }

    [Fact]
    public async Task ValidarArchivoAsync_ArchivoFalsificado_EsRechazado()
    {
        // Un archivo de texto plano disfrazado con extensión .pdf
        var contenidoTexto = Encoding.UTF8.GetBytes("Esto no es un PDF real, es un script malicioso");
        var file = CrearFormFile(contenidoTexto, "malicioso.pdf", "application/pdf");

        var resultado = await FileValidationService.ValidarArchivoAsync(file);

        resultado.EsValido.Should().BeFalse();
        resultado.MensajeError.Should().Contain("no coincide con la firma auténtica");
    }

    [Fact]
    public async Task ValidarArchivoAsync_ArchivoMayorA10MB_EsRechazado()
    {
        // Simulamos un IFormFile con longitud > 10MB
        var streamMock = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4"));
        var fileMock = new FormFile(streamMock, 0, (10 * 1024 * 1024) + 1, "file", "enorme.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var resultado = await FileValidationService.ValidarArchivoAsync(fileMock);

        resultado.EsValido.Should().BeFalse();
        resultado.MensajeError.Should().Contain("10 MB");
    }

    [Fact]
    public async Task ValidarArchivoAsync_ArchivoVacio_EsRechazado()
    {
        var file = CrearFormFile(Array.Empty<byte>(), "vacio.pdf", "application/pdf");

        var resultado = await FileValidationService.ValidarArchivoAsync(file);

        resultado.EsValido.Should().BeFalse();
        resultado.MensajeError.Should().Contain("vacío");
    }

    [Fact]
    public async Task ValidarArchivoAsync_ArchivoNulo_EsRechazado()
    {
        var resultado = await FileValidationService.ValidarArchivoAsync(null);

        resultado.EsValido.Should().BeFalse();
        resultado.MensajeError.Should().Contain("No se suministró ningún archivo");
    }
}
