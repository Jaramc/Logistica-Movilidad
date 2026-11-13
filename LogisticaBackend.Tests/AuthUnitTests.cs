using LogisticaBackend.Auth;
using LogisticaBackend.Domain.Entities;
using Shouldly;

namespace LogisticaBackend.Tests;

public class AuthUnitTests
{
    [Fact]
    public void GeneradorPassword_GeneraContrasenasLargasYDistintas()
    {
        var a = GeneradorPassword.Generar();
        var b = GeneradorPassword.Generar();

        a.Length.ShouldBe(20);
        a.ShouldNotBe(b);
        a.Distinct().Count().ShouldBeGreaterThanOrEqualTo(5);
    }

    private static readonly PasswordProhibidaValidator Validador =
        new(Microsoft.Extensions.Options.Options.Create(new LogisticaBackend.Configuration.SeguridadOptions()));

    [Theory]
    // Comunes, con años, números o leetspeak
    [InlineData("Password2026!", false)]
    [InlineData("P4ssw0rd-2025", false)]
    [InlineData("contraseña123456", false)]
    [InlineData("qwertyuiop12", false)]
    [InlineData("Colombia2025!!", false)]
    [InlineData("202520252025", false)]                 // solo años/números
    [InlineData("01/01/2025-12/31", false)]             // fechas
    [InlineData("aaaaaaaaaaaaaa", false)]
    [InlineData("jajajajajajaja", false)]
    // Empresa y sector
    [InlineData("Logística-segura-99", false)]
    [InlineData("mi m0vilidad favorita", false)]
    [InlineData("LYM la mejor empresa", false)]
    // Datos del propio usuario
    [InlineData("juan.perez-2026-segura", false)]      // parte local del correo
    [InlineData("Rodriguez es mi clave", false)]       // apellido
    // Frases aceptables
    [InlineData("caballo bateria grapa", true)]
    [InlineData("Tres tigres comen trigo", true)]
    public async Task PasswordProhibida_RechazaComunesEmpresaAniosYDatosPersonales(string password, bool esperado)
    {
        var usuario = new Usuario { Email = "juan.perez@empresa.co", NombreCompleto = "Juan Rodriguez" };
        var resultado = await Validador.ValidateAsync(null!, usuario, password);
        resultado.Succeeded.ShouldBe(esperado, string.Join(" ", resultado.Errors.Select(e => e.Code)));
    }

    [Fact]
    public async Task GeneradorPassword_SiemprePasaLaPoliticaDeProhibidas()
    {
        var usuario = new Usuario { Email = "ana@empresa.co", NombreCompleto = "Ana Gómez" };
        for (var i = 0; i < 500; i++)
        {
            (await Validador.ValidateAsync(null!, usuario, GeneradorPassword.Generar())).Succeeded.ShouldBeTrue();
        }
    }

    [Fact]
    public void Totp_CalculaElVectorDePruebaDelRfc6238()
    {
        // RFC 6238, apéndice B: clave "12345678901234567890", T = 59 s -> 94287082 (8 dígitos) -> 287082 (6).
        var clave = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");
        TotpSinReutilizacionProvider.Calcular(clave, 59 / 30).ShouldBe("287082");
        Base32.Decodificar("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ").ShouldBe(clave);
    }

    [Fact]
    public void Roles_SoloAceptaRolesDefinidos()
    {
        Roles.EsValido(Roles.Administrador).ShouldBeTrue();
        Roles.EsValido("Conductor").ShouldBeFalse();
        Roles.EsValido(null).ShouldBeFalse();
    }

    [Fact]
    public void UsuarioStore_NormalizaCodigosDeRecuperacion()
    {
        UsuarioStore.NormalizarCodigo(" ab12c-3de45 ").ShouldBe("AB12C-3DE45");
    }
}
