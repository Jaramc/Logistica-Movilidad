using System.Text;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LogisticaBackend.Tests;

public class LimitesYArchivosTests
{
    [Fact]
    public void LimitadorPorUsuario_BloqueaElSextoIntentoYDaEspera()
    {
        using var limitador = new LimitadorLoginPorUsuario(Options.Create(new LimitePeticionesOptions()));
        for (var i = 0; i < 5; i++)
        {
            limitador.Intentar("correo:Ana@Empresa.co").ShouldBeNull();
        }
        // Mayúsculas y espacios no evaden el límite.
        var espera = limitador.Intentar(" correo:ana@empresa.co ");
        espera.ShouldNotBeNull();
        espera!.Value.ShouldBeInRange(1, 60);
        limitador.Intentar("correo:otro@empresa.co").ShouldBeNull();
    }

    private static IFormFile Archivo(byte[] contenido, string nombre, string mime) =>
        new FormFile(new MemoryStream(contenido), 0, contenido.Length, "file", nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = mime,
        };

    [Fact]
    public async Task ArchivoPdfFalso_EsRechazado()
    {
        var falso = Encoding.ASCII.GetBytes("MZ esto es un ejecutable disfrazado");
        var resultado = await FileValidationService.ValidarArchivoAsync(Archivo(falso, "planilla.pdf", "application/pdf"));
        resultado.EsValido.ShouldBeFalse();
    }

    [Fact]
    public async Task PdfSinGuionEnLaFirma_EsRechazado()
    {
        var casi = Encoding.ASCII.GetBytes("%PDFX1.7 contenido");
        (await FileValidationService.ValidarArchivoAsync(Archivo(casi, "a.pdf", "application/pdf"))).EsValido.ShouldBeFalse();
    }

    [Fact]
    public async Task PdfReal_SeAceptaConNombreSanitizado()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%contenido");
        var resultado = await FileValidationService.ValidarArchivoAsync(Archivo(pdf, @"..\..\planilla<script>.pdf", "application/pdf"));
        resultado.EsValido.ShouldBeTrue();
        resultado.NombreParaMostrar.ShouldBe("planillascript.pdf");
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\Users\x\planilla abril.pdf", "planilla abril.pdf")]
    [InlineData("\u0000\u202E.pdf", "pdf")]
    [InlineData("", "archivo")]
    public void SanitizarNombre_QuitaRutasYCaracteresPeligrosos(string entrada, string esperado)
    {
        FileValidationService.SanitizarNombre(entrada).ShouldBe(esperado);
    }
}
