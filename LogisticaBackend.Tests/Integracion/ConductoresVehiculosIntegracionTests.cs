using System.Net;
using LogisticaBackend.Auth;
using LogisticaBackend.Tests.Integracion.Infraestructura;
using Shouldly;

namespace LogisticaBackend.Tests.Integracion;

/// <summary>Bloque 3 de la fase 1: API de catálogos (ConductoresController, VehiculosController).</summary>
[Collection(ColeccionIntegracion.Nombre)]
public class ConductoresVehiculosIntegracionTests
{
    private readonly FabricaApi _fabrica;

    public ConductoresVehiculosIntegracionTests(PostgresFixture postgres) => _fabrica = Fabricas.Principal(postgres);

    private static string CedulaPrueba() => Random.Shared.Next(100_000_000, 999_999_999).ToString();

    [Fact]
    public async Task Crear_ComoRevisor_Rechazado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA", cedula = CedulaPrueba() });
            r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Crear_ComoAdministrador_AsignaCodigoYRechazaCedulaDuplicada()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            var cedula = CedulaPrueba();
            var r = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA DOS", cedula });
            r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            var creado = await r.JsonAsync();
            creado.GetProperty("codigo").GetString().ShouldStartWith("C");
            creado.GetProperty("cedula").GetString().ShouldBe(cedula); // el propio Administrador ve la cédula completa

            var repetido = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "OTRO", cedula });
            repetido.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task ObtenerPorId_ComoRevisor_CedulaEnmascarada()
    {
        var (admin, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        int id;
        string cedula;
        using (admin)
        {
            cedula = CedulaPrueba();
            var r = await admin.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA TRES", cedula });
            id = (await r.JsonAsync()).GetProperty("id").GetInt32();
        }

        var (revisor, _) = await Sembrador.SesionAsync(_fabrica, Roles.Revisor);
        using (revisor)
        {
            var r = await revisor.GetAsync($"/api/conductores/{id}");
            var dto = await r.JsonAsync();
            var vista = dto.GetProperty("cedula").GetString();
            vista.ShouldNotBe(cedula);
            vista.ShouldEndWith(cedula[^4..]);
            dto.GetProperty("tieneMovimientos").GetBoolean().ShouldBeFalse();
            dto.GetProperty("saldo").GetDecimal().ShouldBe(0m);
        }
    }

    [Fact]
    public async Task SaldoInicial_SoloUnaVez_SegundaDevuelve409()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA CUATRO", cedula = CedulaPrueba() });
            var id = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var saldo = await cliente.PostAsync($"/api/conductores/{id}/saldo-inicial",
                new { valor = -63_500m, fechaCorte = "2026-06-01", justificacion = "Saldo inicial de prueba" });
            saldo.StatusCode.ShouldBe(HttpStatusCode.Created, await saldo.Content.ReadAsStringAsync());
            (await saldo.JsonAsync()).GetProperty("saldoResultante").GetDecimal().ShouldBe(-63_500m);

            var segunda = await cliente.PostAsync($"/api/conductores/{id}/saldo-inicial",
                new { valor = 100m, fechaCorte = "2026-06-01", justificacion = "Otra vez" });
            segunda.StatusCode.ShouldBe(HttpStatusCode.Conflict);

            var detalle = await cliente.GetAsync($"/api/conductores/{id}");
            (await detalle.JsonAsync()).GetProperty("saldo").GetDecimal().ShouldBe(-63_500m);
        }
    }

    [Fact]
    public async Task Pagos_DescuentoNominaSumaYPagoEmpresaResta()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA CINCO", cedula = CedulaPrueba() });
            var id = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var descuento = await cliente.PostAsync($"/api/conductores/{id}/pagos",
                new { tipo = "DescuentoNomina", valor = 1_000m, fecha = "2026-06-01" });
            descuento.StatusCode.ShouldBe(HttpStatusCode.Created, await descuento.Content.ReadAsStringAsync());
            (await descuento.JsonAsync()).GetProperty("saldoResultante").GetDecimal().ShouldBe(1_000m);

            var pago = await cliente.PostAsync($"/api/conductores/{id}/pagos",
                new { tipo = "PagoEmpresa", valor = 300m, fecha = "2026-06-02" });
            (await pago.JsonAsync()).GetProperty("saldoResultante").GetDecimal().ShouldBe(700m);

            var movimientos = await cliente.GetAsync($"/api/conductores/{id}/movimientos");
            (await movimientos.JsonAsync()).GetProperty("totalCount").GetInt32().ShouldBe(2);
        }
    }

    [Fact]
    public async Task Ajuste_SinJustificacion_Rechazado422()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            var r = await cliente.PostAsync("/api/conductores", new { nombreCompleto = "CONDUCTOR PRUEBA SEIS", cedula = CedulaPrueba() });
            var id = (await r.JsonAsync()).GetProperty("id").GetInt32();

            var ajuste = await cliente.PostAsync($"/api/conductores/{id}/ajustes", new { valor = 100m, justificacion = "" });
            ajuste.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }
    }

    [Fact]
    public async Task Vehiculo_Crear_RechazaPlacaInvalidaYDuplicada()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            // "123ABC" no es válida ni mayúsculas/minúsculas: Placa.Normalizar solo quita espacios y guiones.
            var invalida = await cliente.PostAsync("/api/vehiculos", new { placa = "123ABC" });
            invalida.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

            var placa = "ZZZ" + Random.Shared.Next(100, 999);
            var creado = await cliente.PostAsync("/api/vehiculos", new { placa });
            creado.StatusCode.ShouldBe(HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());

            var repetido = await cliente.PostAsync("/api/vehiculos", new { placa });
            repetido.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Vehiculo_Desactivar_CambiaElEstado()
    {
        var (cliente, _) = await Sembrador.SesionAsync(_fabrica, Roles.Administrador);
        using (cliente)
        {
            var placa = "YYY" + Random.Shared.Next(100, 999);
            var creado = await cliente.PostAsync("/api/vehiculos", new { placa });
            var id = (await creado.JsonAsync()).GetProperty("id").GetInt32();

            var desactivado = await cliente.PostAsync($"/api/vehiculos/{id}/desactivar");
            desactivado.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await desactivado.JsonAsync()).GetProperty("activo").GetBoolean().ShouldBeFalse();
        }
    }
}
