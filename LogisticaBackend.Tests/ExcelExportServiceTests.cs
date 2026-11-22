using ClosedXML.Excel;
using Shouldly;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LogisticaBackend.Tests;

public class ExcelExportServiceTests
{
    private readonly Mock<ILogger<ExcelExportService>> _loggerMock = new();

    private (ExcelExportService Service, AppDbContext Context) CrearServiceConDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        var service = new ExcelExportService(context, _loggerMock.Object);
        return (service, context);
    }

    [Fact]
    public async Task GenerarConsolidadoExcelAsync_SinRegistros_GeneraEncabezadosY35Columnas()
    {
        var (service, _) = CrearServiceConDb();

        using var stream = await service.GenerarConsolidadoExcelAsync();

        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(0);

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Consolidado Planillas");
        worksheet.ShouldNotBeNull();

        // Verificar columnas A a AI (35 columnas)
        worksheet.LastColumnUsed()?.ColumnNumber().ShouldBe(35);

        // Fila 1 debe tener encabezados
        worksheet.Cell(1, 1).GetString().ShouldBe("ID");
        worksheet.Cell(1, 4).GetString().ShouldBe("Conductor");
        worksheet.Cell(1, 5).GetString().ShouldBe("Placa");
        worksheet.Cell(1, 10).GetString().ShouldBe("Peajes"); // Columna J (primera categoría canónica)
        worksheet.Cell(1, 25).GetString().ShouldBe("Otros");  // Columna Y (última categoría canónica)
        worksheet.Cell(1, 32).GetString().ShouldBe("Estado"); // Columna AF
        worksheet.Cell(1, 33).GetString().ShouldBe("Fecha Registro UTC"); // Columna AG
        worksheet.Cell(1, 34).GetString().ShouldBe("Total declarado");    // Columna AH
        worksheet.Cell(1, 35).GetString().ShouldBe("Diferencia");         // Columna AI
    }

    [Fact]
    public async Task GenerarConsolidadoExcelAsync_ConDiferencias_MuestraTotalDeclaradoYResaltaLaFilaEnAmbar()
    {
        var (service, context) = CrearServiceConDb();
        context.Planillas.AddRange(
            new PlanillaViaje
            {
                Id = 201, Placa = "STW514", Conductor = "PEDRO", FechaInicial = new DateOnly(2026, 6, 13), FechaFinal = new DateOnly(2026, 6, 19),
                TotalGastos = 2_819_200m, TotalGastosDeclarado = 2_869_200m, TieneDiferencias = true,
                EstadoRevision = EstadoRevision.Aprobado, CreadoEn = DateTime.UtcNow,
            },
            new PlanillaViaje
            {
                Id = 202, Placa = "STW514", Conductor = "PEDRO", FechaInicial = new DateOnly(2026, 6, 1), FechaFinal = new DateOnly(2026, 6, 2),
                TotalGastos = 120_000m, EstadoRevision = EstadoRevision.Aprobado, CreadoEn = DateTime.UtcNow,
            });
        await context.SaveChangesAsync();

        using var stream = await service.GenerarConsolidadoExcelAsync();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet("Consolidado Planillas");

        // Fila 2: la del 13-06 (orden por fecha descendente).
        ws.Cell(2, 1).GetDouble().ShouldBe(201);
        ws.Cell(2, 34).GetDouble().ShouldBe(2_869_200);
        ws.Cell(2, 35).GetDouble().ShouldBe(50_000);
        ws.Cell(2, 1).Style.Fill.BackgroundColor.ShouldBe(XLColor.FromHtml("#FEF3C7"));

        // Fila 3: sin OCR ni diferencias: columnas vacías y sin resaltar.
        ws.Cell(3, 34).IsEmpty().ShouldBeTrue();
        ws.Cell(3, 1).Style.Fill.BackgroundColor.ShouldNotBe(XLColor.FromHtml("#FEF3C7"));
    }

    [Fact]
    public async Task GenerarConsolidadoExcelAsync_ConPlanillaYGastos_MapeaValoresACategoriasCorrectas()
    {
        var (service, context) = CrearServiceConDb();

        var planilla = new PlanillaViaje
        {
            Id = 101,
            Placa = "WCN410",
            Conductor = "JUAN VALDEZ",
            Origen = "BOGOTA",
            Destino = "MEDELLIN",
            FechaInicial = new DateOnly(2026, 11, 1),
            FechaFinal = new DateOnly(2026, 11, 5),
            Flete = 6976800m,
            Anticipo = 2800000m,
            TotalGastos = 1678000m,
            SaldoViaje = -1122000m,
            SaldoAnterior = 0m,
            SaldoTotal = -1122000m,
            EstadoRevision = EstadoRevision.Aprobado,
            CreadoEn = DateTime.UtcNow,
            Gastos = new List<GastoDetalle>
            {
                new() { Concepto = "PEAJES", ConceptoCanonico = "PEAJES", Valor = 1500000m, Orden = 1 },
                new() { Concepto = "PARQUEO", ConceptoCanonico = "PARQUEO", Valor = 178000m, Orden = 2 }
            }
        };

        context.Planillas.Add(planilla);
        await context.SaveChangesAsync();

        using var stream = await service.GenerarConsolidadoExcelAsync();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Consolidado Planillas");

        // Fila 2 contiene los datos
        worksheet.Cell(2, 1).GetDouble().ShouldBe(101);
        worksheet.Cell(2, 4).GetString().ShouldBe("JUAN VALDEZ");
        worksheet.Cell(2, 5).GetString().ShouldBe("WCN410");

        // Columna PEAJES (J = 10)
        worksheet.Cell(2, 10).GetDouble().ShouldBe(1500000);
        // Columna PARQUEO (M = 13)
        worksheet.Cell(2, 13).GetDouble().ShouldBe(178000);

        // ACPM (N = 14) debe ser 0
        worksheet.Cell(2, 14).GetDouble().ShouldBe(0);

        // Total Gastos (Z = 26), Flete (AA = 27), Anticipo (AB = 28), Saldo Viaje (AC = 29)
        worksheet.Cell(2, 26).GetDouble().ShouldBe(1678000);
        worksheet.Cell(2, 27).GetDouble().ShouldBe(6976800);
        worksheet.Cell(2, 28).GetDouble().ShouldBe(2800000);
        worksheet.Cell(2, 29).GetDouble().ShouldBe(-1122000);
    }
}
