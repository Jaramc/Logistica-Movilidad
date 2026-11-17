using System.Net;
using LogisticaBackend.Data;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

[Collection(ColeccionIntegracion.Nombre)]
public class AdministradorInicialTests
{
    private const string Email = "admin@inicial.test";
    private const string PasswordInicial = "nubes verdes sobre el rio";
    private readonly PostgresFixture _postgres;

    public AdministradorInicialTests(PostgresFixture postgres) => _postgres = postgres;

    private FabricaApi Arrancar(string baseDeDatos, string password) =>
        new(_postgres, baseDeDatos, new()
        {
            ["ADMIN_INICIAL_EMAIL"] = Email,
            ["ADMIN_INICIAL_PASSWORD"] = password,
        });

    /// <summary>
    /// Caso real: el administrador configuró el 2FA pero perdió la sesión antes de cambiar la
    /// contraseña. La temporal quedó consumida y no hay otro administrador que le genere una nueva.
    /// Al reiniciar con las variables, debe renovarse y conservar su 2FA.
    /// </summary>
    [Fact]
    public async Task ReinicioRenuevaLaTemporal_SiConfiguro2faPeroNoCambioLaPassword()
    {
        var bd = "admin_inicial_" + Guid.NewGuid().ToString("N")[..8];
        string clave;

        using (var primera = Arrancar(bd, PasswordInicial))
        {
            using var cliente = new ClienteApi(primera);
            await cliente.IniciarSesionAsync(Email, PasswordInicial, claveTotp: null);
            var config = await (await cliente.PostAsync("/api/auth/2fa/configurar")).JsonAsync();
            clave = config.GetProperty("clave").GetString()!.Replace(" ", string.Empty).ToUpperInvariant();
            (await cliente.PostAsync("/api/auth/2fa/verificar", new { codigo = Totp.CodigoActual(clave) })).StatusCode
                .ShouldBe(HttpStatusCode.OK);
            // ...y se pierde la sesión sin cambiar la contraseña.

            using var otro = new ClienteApi(primera);
            (await otro.PostAsync("/api/auth/login", new { email = Email, password = PasswordInicial })).StatusCode
                .ShouldBe(HttpStatusCode.Unauthorized); // temporal consumida
        }

        const string nueva = "barco rojo cerca del faro";
        using var segunda = Arrancar(bd, nueva);
        _ = segunda.Server; // el reinicio ejecuta el inicializador

        using var recuperado = new ClienteApi(segunda);
        // Entra con la temporal renovada y su 2FA de siempre (el paso TOTP siguiente, para no reutilizar el código).
        var login = await recuperado.PostAsync("/api/auth/login", new { email = Email, password = nueva });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.JsonAsync()).GetProperty("requiere2fa").GetBoolean().ShouldBeTrue();

        var siguientePaso = LogisticaBackend.Auth.TotpSinReutilizacionProvider.PasoDe(DateTimeOffset.UtcNow) + 1;
        (await recuperado.PostAsync("/api/auth/login-2fa", new { codigo = Totp.CodigoEnPaso(clave, siguientePaso) })).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        (await recuperado.PostAsync("/api/auth/cambiar-password", new { passwordActual = nueva, passwordNueva = "cafe tinto con pan de yuca" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await recuperado.GetAsync("/api/usuarios")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reinicio_NoTocaAlAdministradorYaActivado()
    {
        var bd = "admin_activo_" + Guid.NewGuid().ToString("N")[..8];
        using (var primera = Arrancar(bd, PasswordInicial))
        {
            await Sembrador.EnScopeAsync(primera, async sp =>
            {
                var db = sp.GetRequiredService<AppDbContext>();
                var admin = await db.Users.SingleAsync(u => u.Email == Email);
                admin.TwoFactorEnabled = true;
                admin.FinalizarPasswordTemporal();
                return await db.SaveChangesAsync();
            });
        }

        using var segunda = Arrancar(bd, "otra frase distinta larga");
        _ = segunda.Server;
        var hash = await Sembrador.EnScopeAsync(segunda, sp => sp.GetRequiredService<AppDbContext>()
            .Users.Where(u => u.Email == Email).Select(u => new { u.DebeCambiarPassword, u.PasswordTemporalExpiraEn }).SingleAsync());
        hash.DebeCambiarPassword.ShouldBeFalse();
        hash.PasswordTemporalExpiraEn.ShouldBeNull();
    }
}
