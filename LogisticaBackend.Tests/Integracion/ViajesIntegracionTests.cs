using System.Net;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

/// <summary>Bloque 4 de la fase 1: API de viajes (ViajesController).</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class ViajesIntegracionTests
{
    private readonly FabricaApi _fabrica;

    public ViajesIntegracionTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private static string CedulaPrueba() => Random.Shared.Next(100_000_000, 999_999_999).ToString();
    private static string CodigoPrueba() => "C" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();
    private static string PlacaPrueba(string prefijo) => prefijo + Random.Shared.Next(100, 999);

    private async Task<(int ConductorId, int VehiculoId)> SembrarCatalogosAsync()
    {
        var cedula = CedulaPrueba();
        var placa = PlacaPrueba("VVV");
        return await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var conductor = new Conductor { Codigo = CodigoPrueba(), NombreCompleto = "CONDUCTOR PRUEBA VIAJE", Cedula = cedula };
            var vehiculo = new Vehiculo { Placa = placa, Tipo = "Tractomula" };
            db.Conductores.Add(conductor);
            db.Vehiculos.Add(vehiculo);
            await db.SaveChangesAsync();
            return (conductor.Id, vehiculo.Id);
        });
    }

    private static object AbrirBody(int conductorId, int vehiculoId) => new
    {
        conductorId, vehiculoId, origen = "BOGOTA", destino = "MEDELLIN", cliente = "CLIENTE PRUEBA",
        flete = 6_976_800m, fechaInicio = "2026-06-13",
    };

    /// <summary>Siembra una planilla ya guardada y ligada al viaje (Bloque 5 todavía no conecta esto por la API).</summary>
    private async Task SembrarPlanillaDelViajeAsync(int viajeId, decimal totalGastos, decimal anticipoDeclarado)
    {
        await Sembrador.EnScopeAsync(_fabrica, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            db.Planillas.Add(new PlanillaViaje
            {
                ViajeId = viajeId,
                Placa = "STW514",
                Conductor = "CONDUCTOR PRUEBA VIAJE",
                Origen = "BOGOTA",
                Destino = "MEDELLIN",
                FechaInicial = new DateOnly(2026, 6, 13),
                FechaFinal = new DateOnly(2026, 6, 19),
                Flete = 6_976_800m,
                Anticipo = anticipoDeclarado,
                TotalGastos = totalGastos,
                EstadoRevision = EstadoRevision.Aprobado,
            });
            await db.SaveChangesAsync();
            return true;
        });
    }

    [Fact]
    public async Task Abrir_SegundoViajeMismoConductorOVehiculo_Rechazado409()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var primero = await cliente.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            primero.StatusCode.ShouldBe(HttpStatusCode.Created, await primero.Content.ReadAsStringAsync());

            var (_, otroVehiculoId) = await SembrarCatalogosAsync();
            var segundoMismoConductor = await cliente.PostAsync("/api/viajes", AbrirBody(conductorId, otroVehiculoId));
            segundoMismoConductor.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            var (otroConductorId, _) = await SembrarCatalogosAsync();
            var segundoMismoVehiculo = await cliente.PostAsync("/api/viajes", AbrirBody(otroConductorId, vehiculoId));
            segundoMismoVehiculo.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Traslados_RegistrarYBloquearDespuesDeAprobado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var r = await cliente.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            var viajeId = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var traslado = await cliente.PostAsync($"/api/viajes/{viajeId}/traslados",
                new { fecha = "2026-06-13", valor = 2_800_000m, medio = "Transferencia" });
            traslado.StatusCode.ShouldBe(HttpStatusCode.Created, await traslado.Content.ReadAsStringAsync());

            await SembrarPlanillaDelViajeAsync(viajeId, totalGastos: 2_869_200m, anticipoDeclarado: 2_800_000m);
            var aprobar = await cliente.PostAsync($"/api/viajes/{viajeId}/aprobar");
            aprobar.StatusCode.ShouldBe(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());

            var trasladoTardio = await cliente.PostAsync($"/api/viajes/{viajeId}/traslados",
                new { fecha = "2026-06-14", valor = 1_000m, medio = "Efectivo" });
            trasladoTardio.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Aprobar_SinPlanillaNiTraslado_Rechazado422()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var r = await cliente.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            var viajeId = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var aprobar = await cliente.PostAsync($"/api/viajes/{viajeId}/aprobar");
            aprobar.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await aprobar.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Aprobar_AnticipoDeclaradoDistintoDeLosTraslados_DevuelveAdvertencia()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var r = await cliente.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            var viajeId = (await r.JsonAsync()).GetProperty("id").GetInt32();

            await cliente.PostAsync($"/api/viajes/{viajeId}/traslados", new { fecha = "2026-06-13", valor = 2_800_000m, medio = "Transferencia" });
            // La planilla declara un anticipo distinto al traslado registrado (2.900.000 vs 2.800.000).
            await SembrarPlanillaDelViajeAsync(viajeId, totalGastos: 2_869_200m, anticipoDeclarado: 2_900_000m);

            var aprobar = await cliente.PostAsync($"/api/viajes/{viajeId}/aprobar");
            aprobar.StatusCode.ShouldBe(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());
            var cuerpo = await aprobar.JsonAsync();
            cuerpo.GetProperty("advertencias")[0].GetString().ShouldNotBeNull().ShouldContain("no coincide con los traslados");
        }
    }

    [Fact]
    public async Task Reabrir_SoloAdministrador_SinMotivo400_YVuelveAAbierto()
    {
        var (administrador, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        var (revisor, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (administrador)
        using (revisor)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var r = await administrador.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            var viajeId = (await r.JsonAsync()).GetProperty("id").GetInt32();
            await administrador.PostAsync($"/api/viajes/{viajeId}/traslados", new { fecha = "2026-06-13", valor = 2_800_000m, medio = "Transferencia" });
            await SembrarPlanillaDelViajeAsync(viajeId, totalGastos: 2_869_200m, anticipoDeclarado: 2_800_000m);
            await administrador.PostAsync($"/api/viajes/{viajeId}/aprobar");

            var comoRevisor = await revisor.PostAsync($"/api/viajes/{viajeId}/reabrir", new { motivo = "Intento" });
            comoRevisor.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            var sinMotivo = await administrador.PostAsync($"/api/viajes/{viajeId}/reabrir", new { motivo = "" });
            sinMotivo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            var reabierto = await administrador.PostAsync($"/api/viajes/{viajeId}/reabrir", new { motivo = "Corregir la planilla" });
            reabierto.StatusCode.ShouldBe(HttpStatusCode.OK, await reabierto.Content.ReadAsStringAsync());
            (await reabierto.JsonAsync()).GetProperty("estado").GetString().ShouldBe("Abierto");

            // Se puede volver a aprobar sin chocar con el índice único (ya no está Abierto/EnRevision duplicado).
            var segundaAprobacion = await administrador.PostAsync($"/api/viajes/{viajeId}/aprobar");
            segundaAprobacion.StatusCode.ShouldBe(HttpStatusCode.OK, await segundaAprobacion.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Reabrir_ConPagosRegistrados_Rechazado409()
    {
        var (administrador, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (administrador)
        {
            var (conductorId, vehiculoId) = await SembrarCatalogosAsync();
            var r = await administrador.PostAsync("/api/viajes", AbrirBody(conductorId, vehiculoId));
            var viajeId = (await r.JsonAsync()).GetProperty("id").GetInt32();
            await administrador.PostAsync($"/api/viajes/{viajeId}/traslados", new { fecha = "2026-06-13", valor = 2_800_000m, medio = "Transferencia" });
            await SembrarPlanillaDelViajeAsync(viajeId, totalGastos: 2_869_200m, anticipoDeclarado: 2_800_000m);
            await administrador.PostAsync($"/api/viajes/{viajeId}/aprobar");

            await administrador.PostAsync($"/api/conductores/{conductorId}/pagos", new { tipo = "PagoEmpresa", valor = 100m, fecha = "2026-06-20", viajeId });

            var reabrir = await administrador.PostAsync($"/api/viajes/{viajeId}/reabrir", new { motivo = "Intento con pagos" });
            reabrir.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }
}
