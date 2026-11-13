using System.Security.Claims;
using Shouldly;
using LogisticaBackend.Controllers;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
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

    private (PlanillasController Controller, AppDbContext Context) CrearControllerConDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()) }, "Prueba"));

        var controller = new PlanillasController(
            _ocrServiceMock.Object,
            _excelServiceMock.Object,
            context,
            new RelojFijo(new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero)),
            _loggerMock.Object
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = usuario }
            }
        };

        return (controller, context);
    }

    private static GuardarPlanillaDto DtoPlanillaReal() => new(
        Placa: "wcn-410",
        Conductor: "PEDRO PEREZ",
        Origen: "BOGOTA",
        Destino: "MEDELLIN",
        FechaInicial: new DateOnly(2026, 11, 1),
        FechaFinal: new DateOnly(2026, 11, 5),
        Manifiesto: "MAN-1234",
        EmpresaTransportadora: "LOGISTICA S.A.S.",
        Flete: 6976800m,
        Anticipo: 2800000m,
        SaldoAnterior: 0m,
        Gastos: new List<GastoItemDto>
        {
            new("PARQUEO", 178000m),
            new("PAPELERIA", 32900m),
            new("PEAJES", 1500000m),
            new("ACPM", 1158300m)
        }
    );

    [Fact]
    public async Task GuardarPlanilla_RecalculaTotalesEnServidorYAsignaAutoria()
    {
        var (controller, context) = CrearControllerConDb();

        var result = await controller.GuardarPlanilla(DtoPlanillaReal(), CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedAtActionResult>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);

        var planilla = await context.Planillas.Include(p => p.Gastos).SingleAsync();
        planilla.Placa.ShouldBe("WCN410"); // normalizada
        planilla.TotalGastos.ShouldBe(2869200m);
        planilla.SaldoViaje.ShouldBe(69200m);
        planilla.SaldoTotal.ShouldBe(69200m);
        planilla.Gastos.Count.ShouldBe(4);
        planilla.CreadoPorUsuarioId.ShouldBe(UsuarioId);
        planilla.RevisadoPorUsuarioId.ShouldBe(UsuarioId);
        planilla.EstadoRevision.ShouldBe(EstadoRevision.Aprobado);
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
    }
}
