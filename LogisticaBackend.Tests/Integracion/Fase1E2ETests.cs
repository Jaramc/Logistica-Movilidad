using System.Net;
using LogisticaBackend.Auth;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

/// <summary>
/// Bloque 8 de la fase 1: punta a punta con la planilla real de junio (Cliente Ejemplo), con
/// datos inventados para el conductor (el repositorio es público: ni el nombre ni la cédula son reales).
/// </summary>
[Collection(ColeccionIntegracion.Nombre)]
public class Fase1E2ETests
{
    private readonly FabricaApi _fabrica;

    public Fase1E2ETests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    [Fact]
    public async Task FlujoCompleto_SaldoInicialViajeYPago_TerminaEnCeroYElSegundoViajeChocaMientrasElPrimeroEstaAbierto()
    {
        var ocr = await PlanillaReal01.ProcesarAsync();
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            // 1. Conductor de prueba con saldo inicial −63.500 (le debe a la empresa).
            var cedula = Random.Shared.Next(100_000_000, 999_999_999).ToString();
            var rConductor = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA UNO", cedula });
            rConductor.StatusCode.ShouldBe(HttpStatusCode.Created, await rConductor.Content.ReadAsStringAsync());
            var conductorId = (await rConductor.JsonAsync()).GetProperty("id").GetInt32();

            var rSaldoInicial = await cliente.PostAsync($"/api/conductores/{conductorId}/saldo-inicial",
                new { valor = -63_500m, fechaCorte = "2026-06-01", justificacion = "Saldo inicial de la prueba E2E de la fase 1" });
            rSaldoInicial.StatusCode.ShouldBe(HttpStatusCode.Created, await rSaldoInicial.Content.ReadAsStringAsync());
            (await rSaldoInicial.JsonAsync()).GetProperty("saldoResultante").GetDecimal().ShouldBe(-63_500m);

            // 2. Abre el viaje en STW514 (una de las 4 placas sembradas).
            var rVehiculos = await cliente.GetAsync("/api/vehiculos");
            var vehiculoId = (await rVehiculos.JsonAsync()).EnumerateArray()
                .Single(v => v.GetProperty("placa").GetString() == "STW514").GetProperty("id").GetInt32();

            var rViaje = await cliente.PostAsync("/api/viajes", new
            {
                conductorId, vehiculoId, origen = "Buenaventura", destino = "Medellin",
                cliente = "Cliente Ejemplo", flete = 6_976_000m, fechaInicio = "2026-06-13",
            });
            rViaje.StatusCode.ShouldBe(HttpStatusCode.Created, await rViaje.Content.ReadAsStringAsync());
            var viajeId = (await rViaje.JsonAsync()).GetProperty("id").GetInt32();

            // 6. Mientras el viaje sigue abierto, un segundo viaje para el mismo vehículo debe dar 409.
            var rSegundoViaje = await cliente.PostAsync("/api/viajes", new
            {
                conductorId, vehiculoId, origen = "Cali", destino = "Bogota",
                cliente = "Otro Cliente", flete = 1_000_000m, fechaInicio = "2026-06-14",
            });
            rSegundoViaje.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            var rTraslado = await cliente.PostAsync($"/api/viajes/{viajeId}/traslados",
                new { fecha = "2026-06-13", valor = 2_800_000m, medio = "Transferencia" });
            rTraslado.StatusCode.ShouldBe(HttpStatusCode.Created, await rTraslado.Content.ReadAsStringAsync());

            // 3. Guarda la planilla real con PEAJES corregido a 82.900 (la suma da 2.869.200).
            var rPlanilla = await cliente.PostAsync("/api/planillas", new
            {
                viajeId, placa = ocr.Placa, origen = ocr.Origen, destino = ocr.Destino,
                fechaInicial = ocr.FechaInicial, fechaFinal = ocr.FechaFinal, manifiesto = ocr.Manifiesto,
                empresaTransportadora = ocr.EmpresaTransportadora, flete = ocr.Flete, anticipo = ocr.Anticipo,
                gastos = ocr.DetalleGastos.Select(g => new
                {
                    concepto = g.Concepto, valor = g.Etiqueta == "PEAJES" ? 82_900m : g.Valor,
                    etiqueta = g.Etiqueta == "" ? null : g.Etiqueta, descripcion = g.Descripcion, categoria = g.CategoriaPropuesta,
                }),
                jsonOcrCrudo = ocr.JsonOcrCrudo,
                totalGastosDeclarado = ocr.TotalGastosDeclarado,
                saldoViajeDeclarado = ocr.SaldoViajeDeclarado?.Valor,
                saldoAnteriorDeclarado = ocr.SaldoAnteriorDeclarado?.Valor,
                saldoNuevoDeclarado = ocr.SaldoNuevoDeclarado?.Valor,
            });
            rPlanilla.StatusCode.ShouldBe(HttpStatusCode.Created, await rPlanilla.Content.ReadAsStringAsync());
            (await rPlanilla.JsonAsync()).GetProperty("data").GetProperty("totalGastos").GetDecimal().ShouldBe(2_869_200m);

            // 4. Aprueba el viaje: saldo de viaje 69.200, saldo del conductor 5.700 (igual al "saldo nuevo" escrito).
            var rAprobar = await cliente.PostAsync($"/api/viajes/{viajeId}/aprobar");
            rAprobar.StatusCode.ShouldBe(HttpStatusCode.OK, await rAprobar.Content.ReadAsStringAsync());
            var aprobado = (await rAprobar.JsonAsync()).GetProperty("viaje");
            aprobado.GetProperty("saldoViaje").GetDecimal().ShouldBe(69_200m);
            aprobado.GetProperty("estado").GetString().ShouldBe("Aprobado");

            var rConductorTrasAprobar = await cliente.GetAsync($"/api/conductores/{conductorId}");
            (await rConductorTrasAprobar.JsonAsync()).GetProperty("saldo").GetDecimal().ShouldBe(5_700m);

            // 5. Un PagoEmpresa de 5.700 deja al conductor en 0 y el viaje en Saldado.
            var rPago = await cliente.PostAsync($"/api/conductores/{conductorId}/pagos",
                new { viajeId, tipo = "PagoEmpresa", valor = 5_700m, fecha = "2026-06-20" });
            rPago.StatusCode.ShouldBe(HttpStatusCode.Created, await rPago.Content.ReadAsStringAsync());
            (await rPago.JsonAsync()).GetProperty("saldoResultante").GetDecimal().ShouldBe(0m);

            var rConductorFinal = await cliente.GetAsync($"/api/conductores/{conductorId}");
            (await rConductorFinal.JsonAsync()).GetProperty("saldo").GetDecimal().ShouldBe(0m);

            var rViajeFinal = await cliente.GetAsync($"/api/viajes/{viajeId}");
            (await rViajeFinal.JsonAsync()).GetProperty("estado").GetString().ShouldBe("Saldado");
        }
    }
}
