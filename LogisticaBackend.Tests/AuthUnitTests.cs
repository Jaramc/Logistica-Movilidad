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

    [Theory]
    [InlineData("juan.perez-2026-segura", false)]   // contiene la parte local del correo
    [InlineData("Rodriguez es mi clave", false)]    // contiene el apellido
    [InlineData("caballo bateria grapa", true)]
    public async Task PasswordSinDatosPersonales_RechazaCorreoYNombre(string password, bool esperado)
    {
        var usuario = new Usuario { Email = "juan.perez@empresa.co", NombreCompleto = "Juan Rodriguez" };
        var resultado = await new PasswordSinDatosPersonalesValidator().ValidateAsync(null!, usuario, password);
        resultado.Succeeded.ShouldBe(esperado);
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
