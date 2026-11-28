using System.Security.Claims;
using Shouldly;
using LogisticaBackend.Configuration;
using LogisticaBackend.Controllers;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Services;
using LogisticaBackend.Tests.Soporte;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LogisticaBackend.Tests;

/// <summary>Pruebas unitarias del controlador (la validación y la autorización se prueban en integración).</summary>
public class PlanillasControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private readonly Mock<IPlanillaOcrService> _ocrServiceMock = new();
    private readonly Mock<IExcelExportService> _excelServiceMock = new();
    private readonly Mock<ILogger<PlanillasController>> _loggerMock = new();
    private readonly Mock<IAuditoriaService> _auditoriaMock = new();
    private readonly Mock<ICuentaConductorService> _cuentaMock = new();

    /// <summary>Servicios mínimos para que el controlador pueda crear ProblemDetails.</summary>
    private static readonly Lazy<IServiceProvider> ServiciosMvc = new(() =>
    {
        var servicios = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(servicios);
        Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions.AddControllers(servicios);
        LogisticaBackend.Middleware.ConfiguracionProblemDetails.AddProblemDetailsLogistica(servicios);
        return Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(servicios);
    });

    private (PlanillasController Controller, AppDbContext Context) CrearControllerConDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        _cuentaMock.Setup(c => c.ObtenerSaldoAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()) }, "Prueba"));

        var controller = new PlanillasController(
            _ocrServiceMock.Object,
            _excelServiceMock.Object,
            context,
            new RelojFijo(new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero)),
            _auditoriaMock.Object,
            _cuentaMock.Object,
            Microsoft.Extensions.Options.Options.Create(new LiquidacionesOptions()),
            _loggerMock.Object
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = usuario, RequestServices = ServiciosMvc.Value }
            }
        };

        return (controller, context);
    }

    /// <summary>Siembra un conductor, un vehículo y un viaje Abierto (bloques 1 y 4 de la fase 1).</summary>
    private static async Task<(int ViajeId, int ConductorId)> SembrarViajeAsync(
        AppDbContext context, string placa = "WCN410", string conductorNombre = "PEDRO PEREZ")
    {
        var conductor = new Conductor { Codigo = "C01", NombreCompleto = conductorNombre, Cedula = "1000000001" };
        var vehiculo = new Vehiculo { Placa = placa, Tipo = "Tractomula" };
        context.Conductores.Add(conductor);
        context.Vehiculos.Add(vehiculo);
        await context.SaveChangesAsync();

        var viaje = new Viaje
        {
            ConductorId = conductor.Id, VehiculoId = vehiculo.Id, Origen = "BOGOTA", Destino = "MEDELLIN",
            Cliente = "CLIENTE PRUEBA", FechaInicio = new DateOnly(2026, 11, 1), Estado = EstadoViaje.Abierto,
        };
        context.Viajes.Add(viaje);
        await context.SaveChangesAsync();
        return (viaje.Id, conductor.Id);
    }

    private static GuardarPlanillaDto DtoPlanillaReal(int viajeId) => new(
        ViajeId: viajeId,
        Origen: "BOGOTA",
        Destino: "MEDELLIN",
        FechaInicial: new DateOnly(2026, 11, 1),
        FechaFinal: new DateOnly(2026, 11, 5),
        Flete: 6976800m,
        Anticipo: 2800000m,
        Gastos: new List<GastoItemDto>
        {
            new("PARQUEO", 178000m),
            new("PAPELERIA", 32900m),
            new("PEAJES", 1500000m),
            new("ACPM", 1158300m)
        },
        Manifiesto: "MAN-1234",
        EmpresaTransportadora: "LOGISTICA S.A.S."
    );

    [Fact]
    public async Task GuardarPlanilla_RecalculaTotalesEnServidorYAsignaAutoria()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, _) = await SembrarViajeAsync(context);

        var result = await controller.GuardarPlanilla(DtoPlanillaReal(viajeId), CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);

        var planilla = await context.Planillas.Include(p => p.Gastos).SingleAsync();
        // Placa y conductor salen del viaje, no del cuerpo de la petición (el DTO ya no los trae).
        planilla.Placa.ShouldBe("WCN410");
        planilla.Conductor.ShouldBe("PEDRO PEREZ");
        planilla.ViajeId.ShouldBe(viajeId);
        planilla.TotalGastos.ShouldBe(2869200m);
        planilla.SaldoAnterior.ShouldBe(0m); // ObtenerSaldoAsync mockeado en 0 (conductor sin movimientos)
        planilla.SaldoViaje.ShouldBe(69200m);
        planilla.SaldoTotal.ShouldBe(69200m);
        planilla.Gastos.Count.ShouldBe(4);
        planilla.CreadoPorUsuarioId.ShouldBe(UsuarioId);
        planilla.RevisadoPorUsuarioId.ShouldBe(UsuarioId);
        planilla.EstadoRevision.ShouldBe(EstadoRevision.Aprobado);
        _auditoriaMock.Verify(a => a.RegistrarAsync(AccionesAuditoria.PlanillaGuardada, true, UsuarioId, "PlanillaViaje",
            planilla.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuardarPlanilla_PrimeraPlanillaDeUnViajeAbierto_LoPasaAEnRevision()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, _) = await SembrarViajeAsync(context);

        await controller.GuardarPlanilla(DtoPlanillaReal(viajeId), CancellationToken.None);

        var viaje = await context.Viajes.SingleAsync(v => v.Id == viajeId);
        viaje.Estado.ShouldBe(EstadoViaje.EnRevision);
    }

    [Fact]
    public async Task GuardarPlanilla_TomaElSaldoDeLaCuentaDelConductor_YPermiteSaldosNegativos()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, conductorId) = await SembrarViajeAsync(context);
        _cuentaMock.Setup(c => c.ObtenerSaldoAsync(conductorId, It.IsAny<CancellationToken>())).ReturnsAsync(-150_000m);

        var result = await controller.GuardarPlanilla(DtoPlanillaReal(viajeId), CancellationToken.None);

        result.ShouldBeOfType<CreatedAtActionResult>();
        var nueva = await context.Planillas.SingleAsync();
        nueva.SaldoAnterior.ShouldBe(-150_000m);
        nueva.SaldoViaje.ShouldBe(69_200m);
        nueva.SaldoTotal.ShouldBe(-80_800m); // los saldos pueden quedar negativos
    }

    [Fact]
    public async Task GuardarPlanilla_SaldoAnteriorDeclaradoDifiereDelSaldoRealDelConductor_Advierte()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, conductorId) = await SembrarViajeAsync(context);
        _cuentaMock.Setup(c => c.ObtenerSaldoAsync(conductorId, It.IsAny<CancellationToken>())).ReturnsAsync(-150_000m);

        var result = await controller.GuardarPlanilla(
            DtoPlanillaReal(viajeId) with { SaldoAnteriorDeclarado = -63_500m }, CancellationToken.None);

        var cuerpo = result.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<PlanillaGuardadaDto>();
        cuerpo.Advertencias!.ShouldContain(a => a.Contains("difiere del saldo actual"));
        var nueva = await context.Planillas.SingleAsync();
        nueva.SaldoAnterior.ShouldBe(-150_000m); // manda el saldo real, no el declarado
    }

    [Fact]
    public async Task GuardarPlanilla_PlacaLeidaPorElOcrDistintaDeLaDelViaje_Advierte()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, _) = await SembrarViajeAsync(context, placa: "WCN410");

        var result = await controller.GuardarPlanilla(DtoPlanillaReal(viajeId) with { Placa = "ZZZ999" }, CancellationToken.None);

        var cuerpo = result.ShouldBeOfType<CreatedAtActionResult>().Value.ShouldBeOfType<PlanillaGuardadaDto>();
        cuerpo.Advertencias!.ShouldContain(a => a.Contains("ZZZ999") && a.Contains("WCN410"));
        (await context.Planillas.SingleAsync()).Placa.ShouldBe("WCN410"); // la del viaje manda, no la del OCR
    }

    [Fact]
    public async Task GuardarPlanilla_ViajeInexistente_Retorna404()
    {
        var (controller, _) = CrearControllerConDb();

        var result = await controller.GuardarPlanilla(DtoPlanillaReal(999_999), CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GuardarPlanilla_ViajeYaTienePlanilla_Retorna409()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, _) = await SembrarViajeAsync(context);
        (await controller.GuardarPlanilla(DtoPlanillaReal(viajeId), CancellationToken.None))
            .ShouldBeOfType<CreatedAtActionResult>();

        var segunda = await controller.GuardarPlanilla(DtoPlanillaReal(viajeId), CancellationToken.None);

        segunda.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await context.Planillas.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task GuardarPlanilla_SaldoFueraDelTope_Retorna422()
    {
        var (controller, context) = CrearControllerConDb();
        var (viajeId, conductorId) = await SembrarViajeAsync(context);
        _cuentaMock.Setup(c => c.ObtenerSaldoAsync(conductorId, It.IsAny<CancellationToken>())).ReturnsAsync(-100_000_000m);

        // -100.000.000 + 69.200 está dentro; con un anticipo mayor el saldo total supera el tope negativo.
        var result = await controller.GuardarPlanilla(DtoPlanillaReal(viajeId) with { Anticipo = 3_000_000m }, CancellationToken.None);

        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        (await context.Planillas.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ObtenerPlanillaPorId_IdInexistente_Retorna404()
    {
        var (controller, _) = CrearControllerConDb();

        var result = await controller.ObtenerPlanillaPorId(999, CancellationToken.None);

        var obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ObtenerHistorial_FiltraPorPlacaYPagina()
    {
        var (controller, context) = CrearControllerConDb();
        foreach (var placa in new[] { "ABC123", "ABC124", "XYZ999" })
        {
            context.Planillas.Add(new PlanillaViaje
            {
                Placa = placa,
                Conductor = "JUAN",
                FechaInicial = new DateOnly(2026, 11, 1),
                FechaFinal = new DateOnly(2026, 11, 5),
                EstadoRevision = EstadoRevision.Aprobado,
            });
        }
        await context.SaveChangesAsync();

        var result = await controller.ObtenerHistorial(
            new ConsultaPlanillasDto { Pagina = 1, Tamano = 1, Placa = "abc" }, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var pagina = ok.Value.ShouldBeOfType<PagedResultDto<PlanillaResumenDto>>();
        pagina.TotalCount.ShouldBe(2);
        pagina.Items.Count.ShouldBe(1);
        pagina.TotalPages.ShouldBe(2);
    }

    [Fact]
    public void ObtenerCatalogoConceptos_Retorna16CategoriasCanonicas()
    {
        var (controller, _) = CrearControllerConDb();

        var result = controller.ObtenerCatalogoConceptos();

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var list = okResult.Value as IReadOnlyList<ConceptoCanonicoDto>;
        list.ShouldNotBeNull();
        list.Count.ShouldBe(16);
        list.Select(c => c.Nombre).ShouldContain("PEAJES");
        list.Select(c => c.Nombre).ShouldContain("ACPM");
        list.Select(c => c.Nombre).ShouldContain("OTROS");
    }

    [Fact]
    public async Task DescargarConsolidado_RetornaFileStreamResultConMimeExcel()
    {
        var (controller, _) = CrearControllerConDb();

        _excelServiceMock.Setup(e => e.GenerarConsolidadoExcelAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        var result = await controller.DescargarConsolidado(
            new RangoFechasDescargaDto { Desde = new DateOnly(2026, 1, 1), Hasta = new DateOnly(2026, 6, 30) }, CancellationToken.None);

        var fileResult = result.ShouldBeOfType<FileStreamResult>();
        fileResult.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.ShouldBe("Consolidado_Planillas_20260101_20260630.xlsx");
        _auditoriaMock.Verify(a => a.RegistrarAsync(AccionesAuditoria.ConsolidadoDescargado, true, UsuarioId, It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
