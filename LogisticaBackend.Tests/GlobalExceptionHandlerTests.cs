using System.Text.Json;
using Azure;
using LogisticaBackend.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http.Features;
using Shouldly;

namespace LogisticaBackend.Tests;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, string Cuerpo)> ManejarAsync(Exception ex)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetailsLogistica();
        var sp = services.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = sp };
        http.Request.Path = "/api/planillas/123";
        var cuerpo = new MemoryStream();
        http.Response.Body = cuerpo;

        var handler = new GlobalExceptionHandler(sp.GetRequiredService<IProblemDetailsService>(), NullLogger<GlobalExceptionHandler>.Instance);
        (await handler.TryHandleAsync(http, ex, CancellationToken.None)).ShouldBeTrue();

        return (http.Response.StatusCode, System.Text.Encoding.UTF8.GetString(cuerpo.ToArray()));
    }

    [Fact]
    public async Task ErrorInesperado_NoExponeMensajeNiStackTrace()
    {
        var (status, cuerpo) = await ManejarAsync(new InvalidOperationException("Password=secreto123 en tabla usuarios"));

        status.ShouldBe(500);
        cuerpo.ShouldNotContain("secreto123");
        cuerpo.ShouldNotContain("InvalidOperationException");
        cuerpo.ShouldNotContain("at LogisticaBackend");

        using var json = JsonDocument.Parse(cuerpo);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Error interno");
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        json.RootElement.TryGetProperty("instance", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ErrorDeAzure_Devuelve502Generico()
    {
        var (status, cuerpo) = await ManejarAsync(new RequestFailedException(401, "Access denied due to invalid subscription key", "401", null));

        status.ShouldBe(502);
        cuerpo.ShouldNotContain("subscription key");
    }

    [Fact]
    public async Task ConflictoDeConcurrencia_Devuelve409()
    {
        var (status, cuerpo) = await ManejarAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("expected 1 row(s) but affected 0"));

        status.ShouldBe(409);
        cuerpo.ShouldNotContain("affected 0");
        using var json = JsonDocument.Parse(cuerpo);
        json.RootElement.GetProperty("detail").GetString()
            .ShouldBe("El registro fue modificado por otra persona. Recarga e intenta de nuevo.");
    }

    [Fact]
    public async Task SolicitudHttpInvalida_NoExponeMensajeInterno()
    {
        var (status, cuerpo) = await ManejarAsync(new BadHttpRequestException("Unexpected end of request content.", 400));

        status.ShouldBe(400);
        cuerpo.ShouldNotContain("Unexpected end");
    }
}
