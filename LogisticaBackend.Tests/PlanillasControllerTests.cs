using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using LogisticaBackend.Controllers;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LogisticaBackend.Tests;

public class PlanillasControllerTests
{
    private readonly Mock<IPlanillaOcrService> _ocrServiceMock = new();
    private readonly Mock<IExcelExportService> _excelServiceMock = new();
    private readonly Mock<IValidator<GuardarPlanillaDto>> _validatorMock = new();
    private readonly Mock<ILogger<PlanillasController>> _loggerMock = new();

    private (PlanillasController Controller, AppDbContext Context) CrearControllerConDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var controller = new PlanillasController(
            _ocrServiceMock.Object,
            _excelServiceMock.Object,
            _validatorMock.Object,
            _loggerMock.Object,
            context
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, context);
    }

    [Fact]
    public async Task GuardarPlanilla_DiferenciaEnTotalesMayorAUnCentavo_Retorna422ProblemDetails()
    {
        var (controller, _) = CrearControllerConDb();

        // Total gastos enviado: 2.869.200, pero gastos reales suman: 1.000.000 (Diferencia > 0.01)
        var dto = new GuardarPlanillaDto(
            Placa: "WCN410",
            Conductor: "PEDRO PEREZ",
            Origen: "BOGOTA",
            Destino: "MEDELLIN",
            FechaInicial: new DateOnly(2026, 11, 1),
            FechaFinal: new DateOnly(2026, 11, 5),
            Flete: 6976800m,
            Anticipo: 2800000m,
            TotalGastos: 2869200m, // Difiere deliberadamente de la suma de renglones
            SaldoViaje: 69200m,
            SaldoAnterior: 0m,
            SaldoTotal: 69200m,
            Gastos: new List<GastoItemDto>
            {
                new("PARQUEO", 1000000m) // Solo suma 1.000.000
            },
            Manifiesto: "MAN-1",
            EmpresaTransportadora: "LOGISTICA"
        );

        _validatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var result = await controller.GuardarPlanilla(dto, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        objResult.Value.Should().BeOfType<ProblemDetails>();

        var problemDetails = (ProblemDetails)objResult.Value!;
        problemDetails.Title.Should().Be("Discrepancia en Totales Contables");
        problemDetails.Detail.Should().Contain("TotalGastos");
    }

    [Fact]
    public async Task GuardarPlanilla_DatosValidosYTotalesCuadrados_PersisteEnBaseDeDatosYRetorna201()
    {
        var (controller, context) = CrearControllerConDb();

        var dto = new GuardarPlanillaDto(
            Placa: "WCN410",
            Conductor: "PEDRO PEREZ",
            Origen: "BOGOTA",
            Destino: "MEDELLIN",
            FechaInicial: new DateOnly(2026, 11, 1),
            FechaFinal: new DateOnly(2026, 11, 5),
            Flete: 6976800m,
            Anticipo: 2800000m,
            TotalGastos: 2869200m,
            SaldoViaje: 69200m,
            SaldoAnterior: 0m,
            SaldoTotal: 69200m,
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

        _validatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var result = await controller.GuardarPlanilla(dto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);

        // Verificar que realmente se persistió en el contexto
        var planillaEnDb = await context.Planillas.Include(p => p.Gastos).FirstOrDefaultAsync(p => p.Placa == "WCN410");
        planillaEnDb.Should().NotBeNull();
        planillaEnDb!.Placa.Should().Be("WCN410");
        planillaEnDb.Gastos.Should().HaveCount(4);
    }

    [Fact]
    public async Task GuardarPlanilla_ValidacionFalla_Retorna422UnprocessableEntity()
    {
        var (controller, _) = CrearControllerConDb();

        var dto = new GuardarPlanillaDto(
            Placa: "", // Invalida
            Conductor: "",
            Origen: "",
            Destino: "",
            FechaInicial: DateOnly.MinValue,
            FechaFinal: DateOnly.MinValue,
            Flete: 0,
            Anticipo: 0,
            TotalGastos: 0,
            SaldoViaje: 0,
            SaldoAnterior: 0,
            SaldoTotal: 0,
            Gastos: new List<GastoItemDto>(),
            Manifiesto: null,
            EmpresaTransportadora: null
        );

        var failures = new List<ValidationFailure>
        {
            new("Placa", "La placa es obligatoria"),
            new("Conductor", "El conductor es obligatorio")
        };

        _validatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await controller.GuardarPlanilla(dto, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)result;
        objResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        var problem = objResult.Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Title.Should().Be("Error de Validación de Formulario");
    }

    [Fact]
    public async Task ObtenerPlanillaPorId_IdInexistente_Retorna404()
    {
        var (controller, _) = CrearControllerConDb();

        var result = await controller.ObtenerPlanillaPorId(999, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ObtenerHistorial_RetornaPaginadoCorrectamente()
    {
        var (controller, context) = CrearControllerConDb();

        // Agregamos planillas de prueba
        context.Planillas.Add(new PlanillaViaje
        {
            Placa = "ABC123",
            Origen = "BOGOTA",
            Destino = "CALI",
            FechaInicial = new DateOnly(2026, 11, 1),
            FechaFinal = new DateOnly(2026, 11, 5),
            Flete = 1000m,
            Anticipo = 500m,
            TotalGastos = 400m,
            SaldoViaje = -100m,
            SaldoTotal = -100m,
            EstadoRevision = EstadoRevision.Aprobado,
            CreadoEn = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await controller.ObtenerHistorial(page: 1, pageSize: 10, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void ObtenerCatalogoConceptos_Retorna16CategoriasCanonicas()
    {
        var (controller, _) = CrearControllerConDb();

        var result = controller.ObtenerCatalogoConceptos();

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = okResult.Value as IReadOnlyList<ConceptoCanonicoDto>;
        list.Should().NotBeNull();
        list.Should().HaveCount(16);
        list!.Select(c => c.Nombre).Should().Contain("PEAJES");
        list!.Select(c => c.Nombre).Should().Contain("ACPM");
        list!.Select(c => c.Nombre).Should().Contain("OTROS");
    }

    [Fact]
    public async Task DescargarConsolidado_RetornaFileStreamResultConMimeExcel()
    {
        var (controller, _) = CrearControllerConDb();

        _excelServiceMock.Setup(e => e.GenerarConsolidadoExcelAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        var result = await controller.DescargarConsolidado(null, null, CancellationToken.None);

        result.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)result;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().Contain("Consolidado_Planillas");
    }
}
