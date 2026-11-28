using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
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

    private static string CedulaPrueba() => Random.Shared.Next(100_000_000, 999_999_999).ToString();
    private static string CodigoPrueba() => "C" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();
    // 26^3 * 900 combinaciones: la base la comparte toda la colección de pruebas, y con un prefijo
    // fijo (p. ej. "WWW" + 3 dígitos) las colisiones aparecían rápido (solo 900 combinaciones).
    private static string PlacaPrueba() =>
        string.Concat(Enumerable.Range(0, 3).Select(_ => (char)('A' + Random.Shared.Next(26)))) + Random.Shared.Next(100, 999);

    /// <summary>Siembra un conductor, un vehículo y un viaje Abierto (bloques 1 y 4 de la fase 1). Devuelve el ViajeId.</summary>
    private async Task<int> SembrarViajeAsync(string? placa = null, string conductorNombre = "CONDUCTOR PRUEBA")
    {
        placa ??= PlacaPrueba();
        return await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var conductor = new Conductor { Codigo = CodigoPrueba(), NombreCompleto = conductorNombre, Cedula = CedulaPrueba() };
            var vehiculo = new Vehiculo { Placa = placa, Tipo = "Tractomula" };
            db.Conductores.Add(conductor);
            db.Vehiculos.Add(vehiculo);
            await db.SaveChangesAsync();

            var viaje = new Viaje
            {
                ConductorId = conductor.Id, VehiculoId = vehiculo.Id, Origen = "BOGOTA", Destino = "MEDELLIN",
                Cliente = "CLIENTE PRUEBA", FechaInicio = new DateOnly(2026, 11, 1), Estado = EstadoViaje.Abierto,
            };
            db.Viajes.Add(viaje);
            await db.SaveChangesAsync();
            return viaje.Id;
        });
    }

    private static object Planilla(int viajeId, object? extras = null) => new
    {
        viajeId,
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
            var viajeId = await SembrarViajeAsync();
            var malicioso = new
            {
                id = 999_999,
                estadoRevision = "Pendiente",
                totalGastos = 1m,
                saldoViaje = 50_000_000m,
                saldoTotal = 50_000_000m,
                saldoAnterior = 50_000_000m,
                creadoPorUsuarioId = Guid.NewGuid(),
                viajeId, origen = "BOGOTA", destino = "MEDELLIN",
                fechaInicial = "2026-11-01", fechaFinal = "2026-11-05", flete = 6_976_800m, anticipo = 2_800_000m,
                gastos = new[] { new { concepto = "PEAJES", valor = 1_500_000m }, new { concepto = "ACPM", valor = 1_369_200m } },
            };

            var r = await cliente.PostAsync("/api/planillas", malicioso);
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var data = (await r.JsonAsync()).GetProperty("data");

            data.GetProperty("id").GetInt32().ShouldNotBe(999_999);
            data.GetProperty("estadoRevision").GetString().ShouldBe("Aprobado");
            data.GetProperty("totalGastos").GetDecimal().ShouldBe(2_869_200m);
            data.GetProperty("saldoAnterior").GetDecimal().ShouldBe(0m); // conductor recién creado, sin movimientos
            data.GetProperty("saldoViaje").GetDecimal().ShouldBe(69_200m);
            data.GetProperty("saldoTotal").GetDecimal().ShouldBe(69_200m);
            data.GetProperty("creadoPorUsuarioId").GetGuid().ShouldBe(revisor.Id);
            data.GetProperty("revisadoPorUsuarioId").GetGuid().ShouldBe(revisor.Id);
        }
    }

    [Fact]
    public async Task Guardar_TomaElSaldoDeLaCuentaDelConductor_YPermiteSaldosNegativos()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync();
            var conductorId = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
                .Viajes.AsNoTracking().Where(v => v.Id == viajeId).Select(v => v.ConductorId).SingleAsync());
            await Sembrador.EnScopeAsync(_fabrica, async sp =>
            {
                await sp.GetRequiredService<LogisticaBackend.Services.ICuentaConductorService>()
                    .RegistrarSaldoInicialAsync(conductorId, -1_000_000m, new DateOnly(2026, 10, 1), "Saldo inicial de prueba", null);
                return true;
            });

            var r = await cliente.PostAsync("/api/planillas", Planilla(viajeId));
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var data = (await r.JsonAsync()).GetProperty("data");
            data.GetProperty("saldoAnterior").GetDecimal().ShouldBe(-1_000_000m);
            data.GetProperty("saldoTotal").GetDecimal().ShouldBe(-930_800m);

            var id = data.GetProperty("id").GetInt32().ToString();
            var auditadas = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>()
                .Auditoria.CountAsync(a => a.Accion == "PlanillaGuardada" && a.EntidadId == id));
            auditadas.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Guardar_UsaLaCategoriaElegidaEnLaUi_YRechazaCategoriasDesconocidas()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync();
            object Con(object gasto) => new
            {
                viajeId, origen = "Buenaventura", destino = "Medellin", fechaInicial = "2026-06-13", fechaFinal = "2026-06-19",
                flete = 0m, anticipo = 0m, gastos = new[] { gasto },
            };

            // "liga CDA" no tiene palabras clave: sin categoría sería OTROS; el revisor eligió ACPM.
            var r = await cliente.PostAsync("/api/planillas", Con(new { concepto = "ACPM / COMBUSTIBLE", valor = 50_000m, etiqueta = "ACPM / COMBUSTIBLE", descripcion = "liga CDA", categoria = "acpm" }));
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            (await r.JsonAsync()).GetProperty("data").GetProperty("gastos")[0].GetProperty("conceptoCanonico").GetString().ShouldBe("ACPM");

            var otroViajeId = await SembrarViajeAsync();
            object ConOtroViaje(object gasto) => new
            {
                viajeId = otroViajeId, origen = "Buenaventura", destino = "Medellin", fechaInicial = "2026-06-13", fechaFinal = "2026-06-19",
                flete = 0m, anticipo = 0m, gastos = new[] { gasto },
            };
            var invalida = await cliente.PostAsync("/api/planillas", ConOtroViaje(new { concepto = "Repuesto", valor = 10_000m, categoria = "GASOLINA" }));
            invalida.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await invalida.Content.ReadAsStringAsync()).ShouldContain("16 del consolidado");
        }
    }

    [Fact]
    public async Task Guardar_ViajeYaTienePlanilla_Rechazado409()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync();
            var primera = await cliente.PostAsync("/api/planillas", Planilla(viajeId));
            primera.StatusCode.ShouldBe(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());

            var segunda = await cliente.PostAsync("/api/planillas", Planilla(viajeId));
            segunda.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Guardar_ViajeInexistente_Rechazado404()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/planillas", Planilla(9_999_999));
            r.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Guardar_PlacaLeidaPorElOcrDistintaDeLaDelViaje_Advierte()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var placaReal = PlacaPrueba();
            var viajeId = await SembrarViajeAsync(placaReal);

            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, placa = "ZZZ999", origen = "BOGOTA", destino = "MEDELLIN",
                fechaInicial = "2026-11-01", fechaFinal = "2026-11-05", flete = 0m, anticipo = 0m,
                gastos = new[] { new { concepto = "PEAJES", valor = 10_000m } },
            });
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var cuerpo = await r.JsonAsync();
            cuerpo.GetProperty("advertencias")[0].GetString()!.ShouldContain("no coincide con la del viaje");
            cuerpo.GetProperty("data").GetProperty("placa").GetString().ShouldBe(placaReal); // la del viaje manda
        }
    }

    [Theory]
    [InlineData("empresa_transportadora", "'123'", "ck_planillas_viaje_empresa_con_letras")]
    [InlineData("placa", "'stw 514'", "ck_planillas_viaje_placa_formato")]
    [InlineData("fecha_final", "'2026-06-01'", "ck_planillas_viaje_fechas_en_orden")]
    public async Task BaseDeDatos_RechazaConCheckUnInsertDirectoConBasura(string columna, string valor, string restriccion)
    {
        // Aunque alguien salte la API, PostgreSQL no acepta la fila.
        var columnas = new Dictionary<string, string>
        {
            ["fecha_inicial"] = "'2026-06-13'", ["fecha_final"] = "'2026-06-19'", ["placa"] = "'STW514'",
            ["conductor"] = "'CHECK'", ["estado_revision"] = "'Aprobado'", ["empresa_transportadora"] = "'Cliente Ejemplo'",
        };
        columnas[columna] = valor;
        var sql = $"INSERT INTO planillas_viaje ({string.Join(", ", columnas.Keys)}) VALUES ({string.Join(", ", columnas.Values)})";

        var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => Sembrador.EnScopeAsync(_fabrica, sp =>
            sp.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql)));

        error.SqlState.ShouldBe(Npgsql.PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe(restriccion);
    }

    [Fact]
    public async Task PlanillaReal01_AprobadaTalComoLaLeeElOcr_GuardaLos17GastosYLosDeclarados()
    {
        var ocr = await PlanillaReal01.ProcesarAsync();
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync(ocr.Placa);
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, placa = ocr.Placa, origen = ocr.Origen, destino = ocr.Destino,
                fechaInicial = ocr.FechaInicial, fechaFinal = ocr.FechaFinal, manifiesto = ocr.Manifiesto,
                empresaTransportadora = ocr.EmpresaTransportadora, flete = ocr.Flete, anticipo = ocr.Anticipo,
                gastos = ocr.DetalleGastos.Select(g => new
                {
                    concepto = g.Concepto, valor = g.Valor, etiqueta = g.Etiqueta == "" ? null : g.Etiqueta,
                    descripcion = g.Descripcion, categoria = g.CategoriaPropuesta,
                }),
                jsonOcrCrudo = ocr.JsonOcrCrudo,
                totalGastosDeclarado = ocr.TotalGastosDeclarado,
                saldoViajeDeclarado = ocr.SaldoViajeDeclarado?.Valor,
                saldoAnteriorDeclarado = ocr.SaldoAnteriorDeclarado?.Valor,
                saldoNuevoDeclarado = ocr.SaldoNuevoDeclarado?.Valor,
            });
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var id = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var guardada = await Sembrador.EnScopeAsync(_fabrica, sp => sp.GetRequiredService<AppDbContext>().Planillas
                .AsNoTracking().Include(p => p.Gastos).SingleAsync(p => p.Id == id));

            guardada.Gastos.Count.ShouldBe(17);
            guardada.TotalGastos.ShouldBe(2_819_200m);
            guardada.TotalGastosDeclarado.ShouldBe(2_869_200m);
            guardada.SaldoViajeDeclarado.ShouldBe(69_200m);
            guardada.SaldoAnteriorDeclarado.ShouldBe(-63_500m);
            guardada.SaldoNuevoDeclarado.ShouldBe(5_700m);
            guardada.TieneDiferencias.ShouldBeTrue();
            guardada.EmpresaTransportadora.ShouldBe("Cliente Ejemplo");
            guardada.Gastos.Single(g => g.Etiqueta == "COMISION").ShouldSatisfyAllConditions(
                g => g.Descripcion.ShouldBe("2 codos vigias"), g => g.ConceptoCanonico.ShouldBe("COMISION"));
            guardada.Gastos.ShouldNotContain(g => LogisticaBackend.Domain.Services.ListaNegraConceptos.Coincide(g.Concepto));
        }
    }

    [Fact]
    public async Task Guardar_ConValoresDeclarados_LosPersisteYMarcaDiferencias()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync();
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, origen = "Buenaventura", destino = "Medellin", fechaInicial = "2026-06-13", fechaFinal = "2026-06-19",
                empresaTransportadora = "Cliente Ejemplo", flete = 6_976_800m, anticipo = 2_800_000m,
                gastos = new[]
                {
                    new { concepto = "COMISION", valor = 30_000m, etiqueta = "COMISION", descripcion = (string?)"2 codos vigias", categoria = "COMISION" },
                    new { concepto = "PEAJES", valor = 2_789_200m, etiqueta = "PEAJES", descripcion = (string?)null, categoria = "PEAJES" },
                },
                totalGastosDeclarado = 2_869_200m, saldoViajeDeclarado = 69_200m, saldoAnteriorDeclarado = 0m, saldoNuevoDeclarado = 69_200m,
            });
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var data = (await r.JsonAsync()).GetProperty("data");

            data.GetProperty("totalGastos").GetDecimal().ShouldBe(2_819_200m);
            data.GetProperty("totalGastosDeclarado").GetDecimal().ShouldBe(2_869_200m);
            data.GetProperty("saldoViajeDeclarado").GetDecimal().ShouldBe(69_200m);
            data.GetProperty("tieneDiferencias").GetBoolean().ShouldBeTrue();
            data.GetProperty("gastos")[0].GetProperty("etiqueta").GetString().ShouldBe("COMISION");
            data.GetProperty("gastos")[0].GetProperty("descripcion").GetString().ShouldBe("2 codos vigias");
        }
    }

    [Fact]
    public async Task Guardar_ValidaFechasYTopesEnElServidor()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var viajeId = await SembrarViajeAsync();
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, origen = "A", destino = "B",
                fechaInicial = "2099-01-01", fechaFinal = "2098-01-01",
                flete = -1m, anticipo = 20_000_001m, gastos = new[] { new { concepto = "ACPM", valor = -5m } },
            });
            r.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            var errores = (await r.JsonAsync()).GetProperty("errors");
            foreach (var campo in new[] { "FechaInicial", "FechaFinal", "Flete", "Anticipo" })
            {
                errores.TryGetProperty(campo, out _).ShouldBeTrue($"Falta el error de {campo}");
            }
        }
    }

    [Fact]
    public async Task Guardar_SinViajeId_EsRechazado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId = 0, origen = "A", destino = "B", fechaInicial = "2026-06-01", fechaFinal = "2026-06-02",
                flete = 0m, anticipo = 0m, gastos = new[] { new { concepto = "ACPM", valor = 100m } },
            });
            r.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            (await r.JsonAsync()).GetProperty("errors").TryGetProperty("ViajeId", out _).ShouldBeTrue();
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

            // OcrFalso siempre devuelve la placa WCN410: el viaje se siembra con esa misma placa
            // para que esta prueba se concentre en el jsonOcrCrudo, no en la advertencia de placa.
            var viajeId = await SembrarViajeAsync(datos.GetProperty("placa").GetString());
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, placa = datos.GetProperty("placa").GetString(),
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
                var viajeId = await SembrarViajeAsync();
                var r = await cliente.PostAsync("/api/planillas", new
                {
                    viajeId, origen = "BOGOTA", destino = "MEDELLIN",
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
            var viajeId = await SembrarViajeAsync();
            var r = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, origen = "BOGOTA", destino = "MEDELLIN",
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
