using System.Net;
using System.Text.Json;
using LogisticaBackend.Auth;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

[Collection(ColeccionIntegracion.Nombre)]
public class ProteccionEndpointsTests
{
    /// <summary>
    /// ÚNICOS endpoints que pueden responder sin sesión. Si alguien agrega un endpoint anónimo
    /// (o uno sin proteger), estas pruebas fallan.
    /// </summary>
    private static readonly HashSet<string> EndpointsAnonimos = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET /api/auth/antiforgery",
        "POST /api/auth/login",
        "POST /api/auth/login-2fa",
        "POST /api/auth/login-recuperacion",
        "GET /health/live",
        "GET /health/ready",
    };

    private readonly FabricaApi _fabrica;

    public ProteccionEndpointsTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private sealed record EndpointRegistrado(string Metodo, string Ruta, bool PermiteAnonimo)
    {
        public string Clave => $"{Metodo} {Ruta}";
    }

    private List<EndpointRegistrado> RecorrerEndpoints()
    {
        var fuente = _fabrica.Services.GetRequiredService<EndpointDataSource>();
        var lista = new List<EndpointRegistrado>();
        foreach (var endpoint in fuente.Endpoints.OfType<RouteEndpoint>())
        {
            var metodos = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" };
            var ruta = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
            var anonimo = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            lista.AddRange(metodos.Select(m => new EndpointRegistrado(m, ruta, anonimo)));
        }
        lista.Count.ShouldBeGreaterThan(15, "No se encontraron los endpoints de la API.");
        return lista;
    }

    /// <summary>Reemplaza los parámetros de ruta por valores válidos de ejemplo.</summary>
    private static string RutaConcreta(string patron) =>
        System.Text.RegularExpressions.Regex.Replace(patron, @"\{(\w+)(:(\w+))?\??\}", m => m.Groups[3].Value switch
        {
            "int" => "1",
            "guid" => Guid.NewGuid().ToString(),
            _ => "x",
        });

    [Fact]
    public void SoloLosEndpointsDeLaListaBlancaPermitenAnonimos()
    {
        var anonimos = RecorrerEndpoints().Where(e => e.PermiteAnonimo).Select(e => e.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        anonimos.ShouldBe(EndpointsAnonimos, ignoreOrder: true);
    }

    [Fact]
    public async Task SinSesion_TodoEndpointNoAnonimoResponde401()
    {
        using var cliente = new ClienteApi(_fabrica);
        var fallas = new List<string>();

        foreach (var endpoint in RecorrerEndpoints().Where(e => !EndpointsAnonimos.Contains(e.Clave)))
        {
            // Con token CSRF válido: así se comprueba la autenticación y no el antiforgery.
            var respuesta = await cliente.EnviarAsync(new HttpMethod(endpoint.Metodo), RutaConcreta(endpoint.Ruta));
            if (respuesta.StatusCode != HttpStatusCode.Unauthorized)
            {
                fallas.Add($"{endpoint.Clave} -> {(int)respuesta.StatusCode}");
            }
        }

        fallas.ShouldBeEmpty();
    }

    [Fact]
    public async Task UsuarioSin2fa_Recibe403EnNegocio_PeroPuedeConfigurarlo()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Administrador, con2fa: false);
        using var cliente = new ClienteApi(_fabrica);
        await cliente.IniciarSesionAsync(usuario.Email, usuario.Password, claveTotp: null);

        (await cliente.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cliente.GetAsync("/api/usuarios")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cliente.GetAsync("/api/auditoria")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cliente.PostAsync("/api/auth/2fa/regenerar-codigos")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var me = await (await cliente.GetAsync("/api/auth/me")).JsonAsync();
        me.GetProperty("sesionConMfa").GetBoolean().ShouldBeFalse();
        (await cliente.PostAsync("/api/auth/2fa/configurar")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UsuarioConPasswordTemporal_Recibe403EnNegocioAunqueTenga2fa()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Administrador, con2fa: true, debeCambiarPassword: true);
        using var cliente = new ClienteApi(_fabrica);
        await cliente.IniciarSesionAsync(usuario.Email, usuario.Password, usuario.ClaveTotp);

        (await cliente.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Consulta_Recibe403EnGuardarOcrYDescarga()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Consulta);
        using (cliente)
        {
            (await cliente.GetAsync("/api/planillas")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await cliente.PostAsync("/api/planillas", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var pdf = new MultipartFormDataContent();
            var parte = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nx"));
            parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            pdf.Add(parte, "file", "a.pdf");
            var ocr = new HttpRequestMessage(HttpMethod.Post, "/api/planillas/extraer") { Content = pdf };
            ocr.Headers.Add(ConfiguracionAntiforgery.NombreEncabezado, cliente.TokenCsrf);
            (await cliente.Http.SendAsync(ocr)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            (await cliente.GetAsync("/api/planillas/descargar-consolidado?desde=2026-01-01&hasta=2026-06-30")).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Revisor_Recibe403EnDescargaYUsuarios()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            (await cliente.GetAsync("/api/planillas/descargar-consolidado?desde=2026-01-01&hasta=2026-06-30")).StatusCode
                .ShouldBe(HttpStatusCode.Forbidden);
            (await cliente.GetAsync("/api/usuarios")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await cliente.PostAsync("/api/usuarios", new { email = "x@x.co", nombreCompleto = "X", rol = "Administrador" }))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await cliente.GetAsync("/api/auditoria")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Administrador_DescargaConRangoObligatorioYQuedaAuditada()
    {
        var (cliente, admin) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            (await cliente.GetAsync("/api/planillas/descargar-consolidado")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await cliente.GetAsync("/api/planillas/descargar-consolidado?desde=2024-01-01&hasta=2026-01-01")).StatusCode
                .ShouldBe(HttpStatusCode.UnprocessableEntity); // más de 1 año

            var ok = await cliente.GetAsync("/api/planillas/descargar-consolidado?desde=2026-01-01&hasta=2026-06-30");
            ok.StatusCode.ShouldBe(HttpStatusCode.OK);
            ok.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            var auditoria = await (await cliente.GetAsync($"/api/auditoria?accion=ConsolidadoDescargado&usuarioId={admin.Id}")).JsonAsync();
            var registro = auditoria.GetProperty("items")[0];
            registro.GetProperty("detalle").GetString()!.ShouldContain("2026-06-30");
        }
    }

    [Fact]
    public async Task PostSinTokenAntiforgery_EsRechazado()
    {
        using var anonimo = new ClienteApi(_fabrica);
        var login = await anonimo.PostAsync("/api/auth/login", new { email = "a@b.co", password = "x" }, conCsrf: false);
        login.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var sinToken = await cliente.PostAsync("/api/planillas", new { }, conCsrf: false);
            sinToken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await sinToken.JsonAsync()).GetProperty("title").GetString().ShouldBe("Solicitud rechazada");
        }
    }

    [Fact]
    public async Task EncabezadosDeSeguridadPresentes_YSinEncabezadoServer()
    {
        using var cliente = new ClienteApi(_fabrica);
        var r = await cliente.GetAsync("/api/auth/me"); // 401, pero también debe llevar los encabezados

        string Encabezado(string nombre) => r.Headers.TryGetValues(nombre, out var v) ? string.Join(",", v)
            : r.Content.Headers.TryGetValues(nombre, out var c) ? string.Join(",", c) : string.Empty;

        Encabezado("X-Content-Type-Options").ShouldBe("nosniff");
        Encabezado("X-Frame-Options").ShouldBe("DENY");
        Encabezado("Referrer-Policy").ShouldBe("no-referrer");
        Encabezado("Content-Security-Policy").ShouldContain("default-src 'none'");
        Encabezado("Content-Security-Policy").ShouldContain("frame-ancestors 'none'");
        Encabezado("Permissions-Policy").ShouldContain("camera=()");
        Encabezado("Strict-Transport-Security").ShouldContain("max-age=");
        r.Headers.CacheControl!.NoStore.ShouldBeTrue();
        r.Headers.Contains("Server").ShouldBeFalse();

        // TestServer no usa Kestrel: se verifica también la configuración real de Kestrel.
        _fabrica.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.AddServerHeader.ShouldBeFalse();
    }

    [Fact]
    public async Task Swagger_NoExisteFueraDeDevelopment()
    {
        using var cliente = new ClienteApi(_fabrica);
        var r = await cliente.GetAsync("/swagger/v1/swagger.json");
        // Fuera de Development la ruta no existe; la política por defecto responde 401 sin revelarlo.
        r.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        (await r.Content.ReadAsStringAsync()).ShouldNotContain("openapi");
    }

    [Fact]
    public async Task CookieDeSesion_TieneLosAtributosSeguros()
    {
        var usuario = await Sembrador.CrearUsuarioAsync(_fabrica, Roles.Consulta);
        using var cliente = new ClienteApi(_fabrica);
        await cliente.PedirTokenCsrfAsync();
        await cliente.PostAsync("/api/auth/login", new { email = usuario.Email, password = usuario.Password });
        var r = await cliente.PostAsync("/api/auth/login-2fa", new { codigo = Totp.CodigoActual(usuario.ClaveTotp!) });

        var setCookie = string.Join("\n", r.Headers.GetValues("Set-Cookie"));
        var sesion = setCookie.Split('\n').Single(c => c.StartsWith(ConfiguracionIdentidad.NombreCookieSesion + "="));
        sesion.ShouldContain("secure", Case.Insensitive);
        sesion.ShouldContain("httponly", Case.Insensitive);
        sesion.ShouldContain("samesite=strict", Case.Insensitive);
        sesion.ShouldContain("path=/", Case.Insensitive);
        sesion.ShouldNotContain("expires=", Case.Insensitive); // cookie de sesión del navegador
        sesion.ShouldNotContain("domain=", Case.Insensitive);
    }
}
