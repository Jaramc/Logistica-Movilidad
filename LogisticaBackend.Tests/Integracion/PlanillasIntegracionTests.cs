using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

[Collection(ColeccionIntegracion.Nombre)]
public class PlanillasIntegracionTests
{
    private readonly FabricaApi _fabrica;

    public PlanillasIntegracionTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private static object Planilla(string conductor, object? extras = null) => new
    {
        placa = "wcn-410",
        conductor,
        origen = "BOGOTA",
        destino = "MEDELLIN",
        fechaInicial = "2026-11-01",
        fechaFinal = "2026-11-05",
        manifiesto = "MAN-1",
        empresaTransportadora = "TRANSPORTES S.A.S.",
        flete = 6_976_800m,
        anticipo = 2_800_000m,
        gastos = new[]
        {
            new { concepto = "PARQUEO", valor = 178_000m },
            new { concepto = "PAPELERIA", valor = 32_900m },
            new { concepto = "PEAJES", valor = 1_500_000m },
            new { concepto = "ACPM", valor = 1_158_300m },
        },
    };

    [Fact]
    public async Task Guardar_IgnoraIdEstadoYTotalesDelCliente_YRecalcula()
    {
        var (cliente, revisor) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var conductor = "CONDUCTOR " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var malicioso = new
            {
                id = 999_999,
                estadoRevision = "Pendiente",
                totalGastos = 1m,
                saldoViaje = 50_000_000m,
                saldoTotal = 50_000_000m,
                saldoAnterior = 50_000_000m,
                creadoPorUsuarioId = Guid.NewGuid(),
                placa = "WCN410", conductor, origen = "BOGOTA", destino = "MEDELLIN",
                fechaInicial = "2026-11-01", fechaFinal = "2026-11-05", flete = 6_976_800m, anticipo = 2_800_000m,
                gastos = new[] { new { concepto = "PEAJES", valor = 1_500_000m }, new { concepto = "ACPM", valor = 1_369_200m } },
            };

            var r = await cliente.PostAsync("/api/planillas", malicioso);
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var data = (await r.JsonAsync()).GetProperty("data");

            data.GetProperty("id").GetInt32().ShouldNotBe(999_999);
            data.GetProperty("estadoRevision").GetString().ShouldBe("Aprobado");
            data.GetProperty("totalGastos").GetDecimal().ShouldBe(2_869_200m);
            data.GetProperty("saldoAnterior").GetDecimal().ShouldBe(0m);  // primera planilla del conductor
            data.GetProperty("saldoViaje").GetDecimal().ShouldBe(69_200m);
            data.GetProperty("saldoTotal").GetDecimal().ShouldBe(69_200m);
            data.GetProperty("creadoPorUsuarioId").GetGuid().ShouldBe(revisor.Id);
            data.GetProperty("revisadoPorUsuarioId").GetGuid().ShouldBe(revisor.Id);
        }
    }

    [Fact]
    public async Task Guardar_EncadenaElSaldoAnteriorDelConductor_YPermiteSaldosNegativos()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var conductor = "CADENA " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            // Anticipo mayor que los gastos: saldo del viaje negativo.
            var primera = await cliente.PostAsync("/api/planillas", new
            {
                placa = "ABC123", conductor, origen = "A", destino = "B", fechaInicial = "2026-10-01", fechaFinal = "2026-10-03",
                flete = 0m, anticipo = 3_000_000m, gastos = new[] { new { concepto = "ACPM", valor = 2_000_000m } },
            });
            primera.StatusCode.ShouldBe(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());
            (await primera.JsonAsync()).GetProperty("data").GetProperty("saldoTotal").GetDecimal().ShouldBe(-1_000_000m);

            var segunda = await cliente.PostAsync("/api/planillas", Planilla(conductor.ToLowerInvariant()));
            segunda.StatusCode.ShouldBe(HttpStatusCode.Created);
            var data = (await segunda.JsonAsync()).GetProperty("data");
            data.GetProperty("saldoAnterior").GetDecimal().ShouldBe(-1_000_000m);
            data.GetProperty("saldoTotal").GetDecimal().ShouldBe(-930_800m);

            var id = data.GetProperty("id").GetInt32().ToString();
            var auditadas = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
                .Auditoria.CountAsync(a => a.Accion == "PlanillaGuardada" && a.EntidadId == id));
            auditadas.ShouldBe(1);
        }
    }

    [Fact]
    public async Task SaldoAnterior_DevuelveElSaldoTotalDeLaUltimaPlanilla_YAdvierteSiElEscritoDifiere()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var conductor = "SALDOS " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            var sinPlanillas = await (await cliente.Http.GetAsync($"/api/planillas/saldo-anterior?conductor={Uri.EscapeDataString(conductor)}")).JsonAsync();
            sinPlanillas.GetProperty("saldoAnterior").GetDecimal().ShouldBe(0m);
            sinPlanillas.GetProperty("planillaAnteriorId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

            // Gastos 120.000 sin anticipo: 120.000 a favor del conductor.
            var primera = await cliente.PostAsync("/api/planillas", new
            {
                placa = "STW514", conductor, origen = "Buenaventura", destino = "Urbano", fechaInicial = "2026-06-13", fechaFinal = "2026-06-13",
                flete = 0m, anticipo = 0m, gastos = new[] { new { concepto = "PORCENTAJE", valor = 120_000m } },
            });
            primera.StatusCode.ShouldBe(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());
            var idPrimera = (await primera.JsonAsync()).GetProperty("id").GetInt32();

            var r = await cliente.Http.GetAsync($"/api/planillas/saldo-anterior?conductor={Uri.EscapeDataString("  " + conductor.ToLowerInvariant())}");
            r.StatusCode.ShouldBe(HttpStatusCode.OK);
            var saldo = await r.JsonAsync();
            saldo.GetProperty("conductor").GetString().ShouldBe(conductor);
            saldo.GetProperty("saldoAnterior").GetDecimal().ShouldBe(120_000m);
            saldo.GetProperty("planillaAnteriorId").GetInt32().ShouldBe(idPrimera);

            // En papel dice 63.500 a favor de la empresa; el sistema calcula 120.000 a favor del conductor.
            var segunda = await cliente.PostAsync("/api/planillas", new
            {
                placa = "STW514", conductor, origen = "Buenaventura", destino = "Medellin", fechaInicial = "2026-06-13", fechaFinal = "2026-06-19",
                flete = 0m, anticipo = 0m, gastos = new[] { new { concepto = "PEAJES", valor = 10_000m } },
                saldoAnteriorDeclarado = -63_500m,
            });
            segunda.StatusCode.ShouldBe(HttpStatusCode.Created, await segunda.Content.ReadAsStringAsync());
            var cuerpo = await segunda.JsonAsync();
            cuerpo.GetProperty("data").GetProperty("saldoAnterior").GetDecimal().ShouldBe(120_000m); // no se sobrescribe
            cuerpo.GetProperty("advertencias")[0].GetString()!.ShouldContain("$63.500 a favor de la empresa");

            var vacio = await cliente.Http.GetAsync("/api/planillas/saldo-anterior?conductor=");
            vacio.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
    }

    [Fact]
    public async Task Guardar_ValidaPlacaFechasYTopesEnElServidor()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/planillas", new
            {
                placa = "12345", conductor = "X", origen = "A", destino = "B",
                fechaInicial = "2099-01-01", fechaFinal = "2098-01-01",
                flete = -1m, anticipo = 20_000_001m, gastos = new[] { new { concepto = "ACPM", valor = -5m } },
            });
            r.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            var errores = (await r.JsonAsync()).GetProperty("errors");
            foreach (var campo in new[] { "Placa", "FechaInicial", "FechaFinal", "Flete", "Anticipo" })
            {
                errores.TryGetProperty(campo, out _).ShouldBeTrue($"Falta el error de {campo}");
            }
        }
    }

    private static MultipartFormDataContent Archivo(byte[] contenido, string nombre, string mime)
    {
        var form = new MultipartFormDataContent();
        var parte = new ByteArrayContent(contenido);
        parte.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(parte, "file", nombre);
        return form;
    }

    private static async Task<HttpResponseMessage> SubirAsync(ClienteApi cliente, MultipartFormDataContent contenido)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, "/api/planillas/extraer") { Content = contenido };
        mensaje.Headers.Add(ConfiguracionAntiforgery.NombreEncabezado, cliente.TokenCsrf);
        return await cliente.Http.SendAsync(mensaje);
    }

    [Fact]
    public async Task ArchivoConExtensionPdfPeroContenidoFalso_EsRechazado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var falso = Encoding.ASCII.GetBytes("MZ\x90\x00 esto es un ejecutable, no un PDF");
            var r = await SubirAsync(cliente, Archivo(falso, "planilla.pdf", "application/pdf"));
            r.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);

            var real = await SubirAsync(cliente, Archivo(Encoding.ASCII.GetBytes("%PDF-1.7\n%contenido"), "planilla.pdf", "application/pdf"));
            real.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task ErrorForzado_DevuelveProblemDetailsSinElMensajeDeLaExcepcion()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            _fabrica.Ocr.ExcepcionAForzar = new InvalidOperationException("Password=secreto-interno en servidor-db-01");
            try
            {
                var r = await SubirAsync(cliente, Archivo(Encoding.ASCII.GetBytes("%PDF-1.7\nx"), "a.pdf", "application/pdf"));
                r.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
                r.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
                var cuerpo = await r.Content.ReadAsStringAsync();
                cuerpo.ShouldNotContain("secreto-interno");
                cuerpo.ShouldNotContain("InvalidOperationException");
                cuerpo.ShouldNotContain("   at ");
                (await r.JsonAsync()).GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
            }
            finally
            {
                _fabrica.Ocr.ExcepcionAForzar = null;
            }
        }
    }

    /// <summary>
    /// TestServer no aplica los límites de Kestrel, así que se verifica la configuración efectiva:
    /// 1 MB global y 10 MB (+64 KB de multipart) solo en el endpoint que sube archivos.
    /// </summary>
    [Fact]
    public void LimitesDeTamano_1MBGlobalY10MBSoloEnLaSubida()
    {
        var kestrel = _fabrica.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>>().Value;
        kestrel.Limits.MaxRequestBodySize.ShouldBe(1024 * 1024);
        _fabrica.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Features.FormOptions>>()
            .Value.MultipartBodyLengthLimit.ShouldBe(1024 * 1024);

        var endpoints = _fabrica.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints;
        var conLimiteAmpliado = endpoints
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()?.MaxRequestBodySize > 1024 * 1024)
            .Select(e => ((Microsoft.AspNetCore.Routing.RouteEndpoint)e).RoutePattern.RawText)
            .ToList();
        conLimiteAmpliado.ShouldBe(new[] { "api/Planillas/extraer" });
        endpoints.Single(e => ((Microsoft.AspNetCore.Routing.RouteEndpoint)e).RoutePattern.RawText == "api/Planillas/extraer")
            .Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>()!.MaxRequestBodySize
            .ShouldBe(10 * 1024 * 1024 + 64 * 1024);
    }

    private Task<string?> JsonOcrGuardadoAsync(int id) => Sembrador.EnScopeAsync(_fabrica, sp =>
        sp.GetRequiredService<AppDbContext>().Planillas.AsNoTracking()
            .Where(p => p.Id == id).Select(p => p.JsonOcrCrudo).SingleAsync());

    /// <summary>Extraer → guardar con el jsonOcrCrudo que devolvió el OCR → queda en la columna jsonb.</summary>
    [Fact]
    public async Task ExtraerYGuardar_PersisteElJsonOcrCrudoDevueltoPorElOcr()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
            var ocr = await SubirAsync(cliente, Archivo(png, "planilla.png", "image/png"));
            ocr.StatusCode.ShouldBe(HttpStatusCode.OK, await ocr.Content.ReadAsStringAsync());
            var datos = (await ocr.JsonAsync()).GetProperty("data");
            datos.GetProperty("camposNoEncontrados").EnumerateArray().Select(e => e.GetString()).ShouldBe(new[] { "Flete" });
            datos.GetProperty("confianzaPromedio").GetSingle().ShouldBe(0.85f);
            var jsonOcr = datos.GetProperty("jsonOcrCrudo").GetString();

            var conductor = "CONDUCTOR " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var r = await cliente.PostAsync("/api/planillas", new
            {
                placa = datos.GetProperty("placa").GetString(), conductor,
                origen = datos.GetProperty("origen").GetString(), destino = datos.GetProperty("destino").GetString(),
                fechaInicial = "2026-11-01", fechaFinal = "2026-11-05",
                flete = 6_976_800m, anticipo = datos.GetProperty("anticipo").GetDecimal(),
                gastos = datos.GetProperty("detalleGastos").EnumerateArray()
                    .Select(g => new { concepto = g.GetProperty("concepto").GetString(), valor = g.GetProperty("valor").GetDecimal() }),
                jsonOcrCrudo = jsonOcr,
            });
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var id = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var guardado = await JsonOcrGuardadoAsync(id);
            using var real = System.Text.Json.JsonDocument.Parse(guardado!);
            // jsonb reordena claves y espacios: se compara campo a campo.
            real.RootElement.GetArrayLength().ShouldBe(2);
            real.RootElement[0].GetProperty("nombre").GetString().ShouldBe("Placa");
            real.RootElement[1].GetProperty("content").GetString().ShouldBe("$ 2.800.000");
            real.RootElement[1].GetProperty("confidence").GetDecimal().ShouldBe(0.8m);
        }
    }

    [Fact]
    public async Task Guardar_ConJsonOcrCrudoInvalidoOGrande_GuardaNullSinRechazar()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            foreach (var jsonMalo in new[] { "{no es json", "[" + new string(' ', LogisticaBackend.Domain.Services.JsonOcrCrudo.TamanoMaximoBytes) + "]" })
            {
                var conductor = "CONDUCTOR " + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                var r = await cliente.PostAsync("/api/planillas", new
                {
                    placa = "WCN410", conductor, origen = "BOGOTA", destino = "MEDELLIN",
                    fechaInicial = "2026-11-01", fechaFinal = "2026-11-05", flete = 1_000m, anticipo = 0m,
                    gastos = new[] { new { concepto = "PEAJES", valor = 100m } },
                    jsonOcrCrudo = jsonMalo,
                });
                r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
                (await JsonOcrGuardadoAsync((await r.JsonAsync()).GetProperty("id").GetInt32())).ShouldBeNull();
            }
        }
    }

    [Fact]
    public async Task Guardar_ConJsonOcrCrudoSinTokenCsrf_EsRechazado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/planillas", new
            {
                placa = "WCN410", conductor = "SIN CSRF", origen = "BOGOTA", destino = "MEDELLIN",
                fechaInicial = "2026-11-01", fechaFinal = "2026-11-05", flete = 1_000m, anticipo = 0m,
                gastos = new[] { new { concepto = "PEAJES", valor = 100m } },
                jsonOcrCrudo = OcrFalso.JsonOcrCrudoFalso,
            }, conCsrf: false);
            r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await r.JsonAsync()).GetProperty("title").GetString().ShouldBe("Solicitud rechazada");
        }
    }

    [Fact]
    public async Task Auditoria_EsDeSoloInsercionEnLaBaseDeDatos()
    {
        var (admin, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador); // genera eventos de login
        admin.Dispose();

        var ex = await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("UPDATE auditoria SET exito = false")));
        ex.MessageText.ShouldContain("solo inserción");

        await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("DELETE FROM auditoria")));
    }
}
