using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LogisticaBackend.Auth;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion.Infraestructura;

/// <summary>
/// Simula el navegador: guarda cookies (incluidas las Secure, por eso la base es https),
/// pide el token antiforgery y lo envía en X-XSRF-TOKEN como hace el frontend.
/// </summary>
public sealed class ClienteApi : IDisposable
{
    /// <summary>No es "localhost": ASP.NET omite HSTS para localhost y aquí se quiere verificar.</summary>
    public static readonly Uri Base = new("https://api.logistica.test");

    public CookieContainer Cookies { get; } = new();
    public HttpClient Http { get; }

    public ClienteApi(FabricaApi fabrica)
    {
        Http = fabrica.CreateDefaultClient(Base, new CookieContainerHandler(Cookies));
    }

    public string? TokenCsrf => Cookies.GetCookies(Base)[ConfiguracionAntiforgery.NombreCookieJs]?.Value;

    public async Task PedirTokenCsrfAsync()
    {
        var r = await Http.GetAsync("/api/auth/antiforgery");
        r.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>POST/PUT con JSON y token CSRF (lo pide si aún no existe).</summary>
    public async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo = null, bool conCsrf = true)
    {
        if (conCsrf && TokenCsrf is null) await PedirTokenCsrfAsync();
        var mensaje = new HttpRequestMessage(metodo, ruta)
        {
            Content = cuerpo is null ? JsonContent.Create(new { }) : JsonContent.Create(cuerpo, cuerpo.GetType()),
        };
        if (conCsrf) mensaje.Headers.Add(ConfiguracionAntiforgery.NombreEncabezado, TokenCsrf);
        return await Http.SendAsync(mensaje);
    }

    public Task<HttpResponseMessage> PostAsync(string ruta, object? cuerpo = null, bool conCsrf = true) =>
        EnviarAsync(HttpMethod.Post, ruta, cuerpo, conCsrf);

    public Task<HttpResponseMessage> PutAsync(string ruta, object? cuerpo = null) => EnviarAsync(HttpMethod.Put, ruta, cuerpo);

    public Task<HttpResponseMessage> GetAsync(string ruta) => Http.GetAsync(ruta);

    /// <summary>Login con contraseña y, si el usuario tiene 2FA, con el código TOTP calculado desde su clave.</summary>
    public async Task IniciarSesionAsync(string email, string password, string? claveTotp)
    {
        var r = await PostAsync("/api/auth/login", new { email, password });
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var cuerpo = await r.Content.ReadFromJsonAsync<JsonElement>();

        if (cuerpo.GetProperty("requiere2fa").GetBoolean())
        {
            claveTotp.ShouldNotBeNull("El usuario requiere 2FA y la prueba no dio su clave.");
            var r2 = await PostAsync("/api/auth/login-2fa", new { codigo = Totp.CodigoActual(claveTotp) });
            r2.StatusCode.ShouldBe(HttpStatusCode.OK, await r2.Content.ReadAsStringAsync());
        }
    }

    public void Dispose() => Http.Dispose();
}

public static class Totp
{
    public static string CodigoActual(string claveBase32) =>
        TotpSinReutilizacionProvider.Calcular(Base32.Decodificar(claveBase32), TotpSinReutilizacionProvider.PasoDe(DateTimeOffset.UtcNow));

    public static string CodigoEnPaso(string claveBase32, long paso) =>
        TotpSinReutilizacionProvider.Calcular(Base32.Decodificar(claveBase32), paso);
}

public static class RespuestaExtensiones
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();
}
