using System.Net;
using LogisticaBackend.Auth;
using LogisticaBackend.Controllers;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

[Collection(ColeccionIntegracion.Nombre)]
public class AutenticacionTests
{
    private readonly PostgresFixture _postgres;
    private readonly FabricaApi _fabrica;

    public AutenticacionTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _fabrica = Fabricas.Principal(postgres);
    }

    private static async Task<string> DetalleAsync(HttpResponseMessage r) =>
        (await r.JsonAsync()).GetProperty("detail").GetString()!;

    [Fact]
    public async Task CuentaSeBloqueaAlSextoIntento_YElMensajeEsIgualParaUsuarioInexistente()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Consulta);
        using var cliente = new ClienteApi(_fabrica);

        var mensajes = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var r = await cliente.PostAsync("/api/auth/login", new { email = usuario.Email, password = "incorrecta-" + i });
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            mensajes.Add(await DetalleAsync(r));
        }

        // Sexto intento con la contraseña CORRECTA: la cuenta ya está bloqueada.
        var bloqueado = await cliente.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password });
        bloqueado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        mensajes.Add(await DetalleAsync(bloqueado));

        var inexistente = await cliente.PostAsync("/api/auth/login", new { email = "nadie@pruebas.co", password = "lo-que-sea-123" });
        inexistente.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        mensajes.Add(await DetalleAsync(inexistente));

        mensajes.Distinct().ShouldHaveSingleItem().ShouldBe(AuthController.MensajeLoginFallido);

        var bloqueadoHasta = await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var um = sp.GetRequiredService<UserManager<Usuario>>();
            return await um.GetLockoutEndDateAsync((await um.FindByIdAsync(usuario.Id.ToString()))!);
        });
        bloqueadoHasta.ShouldNotBeNull();
        bloqueadoHasta.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(14));

        var auditoria = await Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Auditoria.CountAsync(a => a.UsuarioId == usuario.Id && a.Accion == "CuentaBloqueada"));
        auditoria.ShouldBe(1);
    }

    [Fact]
    public async Task LimiteDeLogin_Responde429ConRetryAfter()
    {
        var fabrica = Fabricas.ConLimitesReales(_postgres);
        using var cliente = new ClienteApi(fabrica);

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 6; i++)
        {
            // Correos distintos: se prueba el límite por IP (5/min), no el de usuario.
            ultima = await cliente.PostAsync("/api/auth/login", new { email = $"x{i}@pruebas.co", password = "lo-que-sea-123" });
        }

        ultima!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        ultima.Headers.RetryAfter.ShouldNotBeNull();
        (ultima.Headers.RetryAfter!.Delta ?? TimeSpan.Zero).ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CodigoTotpYaUsado_EsRechazado()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Consulta);

        // Se usa un código del paso actual y se guarda para reutilizarlo.
        var paso = TotpSinReutilizacionProvider.PasoDe(DateTimeOffset.UtcNow);
        var codigo = Totp.CodigoEnPaso(usuario.ClaveTotp!, paso);

        using (var primero = new ClienteApi(_fabrica))
        {
            (await primero.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password })).StatusCode
                .ShouldBe(HttpStatusCode.OK);
            (await primero.PostAsync("/api/auth/login-2fa", new { codigo })).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var segundo = new ClienteApi(_fabrica);
        (await segundo.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password })).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        var reutilizado = await segundo.PostAsync("/api/auth/login-2fa", new { codigo });
        reutilizado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // También se rechaza un código de un paso anterior al ya usado.
        var anterior = await segundo.PostAsync("/api/auth/login-2fa", new { codigo = Totp.CodigoEnPaso(usuario.ClaveTotp!, paso - 1) });
        anterior.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaveTotp_SeGuardaCifradaEnLaBaseDeDatos()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Consulta);
        var guardada = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
            .UserTokens.Where(t => t.UserId == usuario.Id && t.Name == "AuthenticatorKey").Select(t => t.Value).SingleAsync());

        guardada.ShouldNotBeNullOrEmpty();
        guardada.ShouldNotBe(usuario.ClaveTotp);
        guardada!.ShouldNotContain(usuario.ClaveTotp!);
    }

    [Fact]
    public async Task CodigosDeRecuperacion_SeGuardanComoHashYSirvenUnaVez()
    {
        var (cliente, usuario) = await Sembrador.SesionAsync(_fabrica, Roles.Consulta);
        string codigo;
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/auth/2fa/regenerar-codigos");
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
            var codigos = (await r.JsonAsync()).GetProperty("codigos").EnumerateArray().Select(c => c.GetString()!).ToList();
            codigos.Count.ShouldBe(10);
            codigo = codigos[0];
        }

        var guardado = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
            .UserTokens.Where(t => t.UserId == usuario.Id && t.Name == "RecoveryCodes").Select(t => t.Value).SingleAsync());
        guardado!.ShouldNotContain(codigo);

        async Task<HttpStatusCode> EntrarConRecuperacion()
        {
            using var c = new ClienteApi(_fabrica);
            await c.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password });
            return (await c.PostAsync("/api/auth/login-recuperacion", new { codigo })).StatusCode;
        }

        (await EntrarConRecuperacion()).ShouldBe(HttpStatusCode.OK);
        (await EntrarConRecuperacion()).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DesactivarUsuario_InvalidaSuSesionAbierta()
    {
        var (victima, usuario) = await Sembrador.SesionAsync(_fabrica, Roles.Consulta);
        var (admin, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (victima)
        using (admin)
        {
            (await victima.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.OK);

            (await admin.PutAsync($"/api/usuarios/{usuario.Id}/estado", new { activo = false })).StatusCode.ShouldBe(HttpStatusCode.OK);

            (await victima.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            // Y no puede volver a entrar (mismo mensaje genérico).
            using var nuevo = new ClienteApi(_fabrica);
            var r = await nuevo.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password });
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await DetalleAsync(r)).ShouldBe(AuthController.MensajeLoginFallido);
        }
    }

    [Fact]
    public async Task Administrador_NoPuedeDesactivarseNiQuitarseElRolSiEsElUltimo()
    {
        var (admin, yo) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (admin)
        {
            (await admin.PutAsync($"/api/usuarios/{yo.Id}/estado", new { activo = false })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task PasswordTemporal_SirveUnaSolaVezYVenceA24Horas()
    {
        var (admin, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        string email, temporal;
        Guid id;
        using (admin)
        {
            var r = await admin.PostAsync("/api/usuarios",
                new { email = $"nuevo{Guid.NewGuid():N}@pruebas.co", nombreCompleto = "Nuevo Revisor", rol = Roles.Revisor });
            r.StatusCode.ShouldBe(HttpStatusCode.Created);
            var json = await r.JsonAsync();
            temporal = json.GetProperty("passwordTemporal").GetString()!;
            email = json.GetProperty("usuario").GetProperty("email").GetString()!;
            id = json.GetProperty("usuario").GetProperty("id").GetGuid();
            json.GetProperty("usuario").GetProperty("pendienteActivacion").GetBoolean().ShouldBeTrue();
        }

        // Primer uso: entra con sesión limitada.
        using (var primero = new ClienteApi(_fabrica))
        {
            await primero.IniciarSesionAsync(email, temporal, claveTotp: null);
            (await primero.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Segundo uso: rechazado con el mensaje genérico.
        using (var segundo = new ClienteApi(_fabrica))
        {
            var r = await segundo.PostAsync("/api/auth/login", new { email, password = temporal });
            r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await DetalleAsync(r)).ShouldBe(AuthController.MensajeLoginFallido);
        }

        // El administrador genera otra; se simula que pasaron más de 24 horas.
        string temporal2;
        var (admin2, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (admin2)
        {
            var r = await admin2.PostAsync($"/api/usuarios/{id}/password-temporal");
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
            temporal2 = (await r.JsonAsync()).GetProperty("passwordTemporal").GetString()!;
        }
        await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var u = await db.Users.SingleAsync(x => x.Id == id);
            u.PasswordTemporalExpiraEn = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        using var vencido = new ClienteApi(_fabrica);
        (await vencido.PostAsync("/api/auth/login", new { email, password = temporal2 })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FlujoCompleto_ConfigurarTotpCambiarPasswordYEntrar()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Revisor, con2fa: false, debeCambiarPassword: true);
        using var cliente = new ClienteApi(_fabrica);
        await cliente.IniciarSesionAsync(usuario.Email, usuario.Password, claveTotp: null);

        var config = await (await cliente.PostAsync("/api/auth/2fa/configurar")).JsonAsync();
        var clave = config.GetProperty("clave").GetString()!.Replace(" ", string.Empty).ToUpperInvariant();
        config.GetProperty("uriOtpauth").GetString()!.ShouldStartWith("otpauth://totp/");

        var verificar = await cliente.PostAsync("/api/auth/2fa/verificar", new { codigo = Totp.CodigoActual(clave) });
        verificar.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await verificar.JsonAsync()).GetProperty("codigos").GetArrayLength().ShouldBe(10);

        // Con 2FA pero contraseña temporal: aún no entra a negocio.
        (await cliente.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await cliente.PostAsync("/api/auth/cambiar-password",
            new { passwordActual = usuario.Password, passwordNueva = "tres tigres comen trigo" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await cliente.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await (await cliente.GetAsync("/api/auth/me")).JsonAsync();
        me.GetProperty("sesionConMfa").GetBoolean().ShouldBeTrue();
        me.GetProperty("debeCambiarPassword").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task CambiarPassword_RechazaContrasenasProhibidas()
    {
        var (cliente, usuario) = await Sembrador.SesionAsync(_fabrica, Roles.Consulta);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/auth/cambiar-password",
                new { passwordActual = usuario.Password, passwordNueva = "Logistica2025!!" });
            r.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
    }
}
