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
    public async Task GenerarConsolidadoExcelAsync_SinRegistros_GeneraEncabezadosY33Columnas()
    {
        var (service, _) = CrearServiceConDb();

        using var stream = await service.GenerarConsolidadoExcelAsync();

        stream.ShouldNotBeNull();
        stream.Length.ShouldBeGreaterThan(0);

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Consolidado Planillas");
        worksheet.ShouldNotBeNull();

        // Verificar columnas A a AG (33 columnas)
        worksheet.LastColumnUsed()?.ColumnNumber().ShouldBe(33);

        // Fila 1 debe tener encabezados
        worksheet.Cell(1, 1).GetString().ShouldBe("ID");
        worksheet.Cell(1, 4).GetString().ShouldBe("Conductor");
        worksheet.Cell(1, 5).GetString().ShouldBe("Placa");
        worksheet.Cell(1, 10).GetString().ShouldBe("Peajes"); // Columna J (primera categoría canónica)
        worksheet.Cell(1, 25).GetString().ShouldBe("Otros");  // Columna Y (última categoría canónica)
        worksheet.Cell(1, 32).GetString().ShouldBe("Estado"); // Columna AF
        worksheet.Cell(1, 33).GetString().ShouldBe("Fecha Registro UTC"); // Columna AG
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
