using ClosedXML.Excel;
using FluentAssertions;
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

        stream.Should().NotBeNull();
        stream.Length.Should().BeGreaterThan(0);

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Consolidado Planillas");
        worksheet.Should().NotBeNull();

        // Verificar columnas A a AG (33 columnas)
        worksheet.LastColumnUsed()?.ColumnNumber().Should().Be(33);

        // Fila 1 debe tener encabezados
        worksheet.Cell(1, 1).GetString().Should().Be("ID");
        worksheet.Cell(1, 4).GetString().Should().Be("Conductor");
        worksheet.Cell(1, 5).GetString().Should().Be("Placa");
        worksheet.Cell(1, 10).GetString().Should().Be("Peajes"); // Columna J (primera categoría canónica)
        worksheet.Cell(1, 25).GetString().Should().Be("Otros");  // Columna Y (última categoría canónica)
        worksheet.Cell(1, 32).GetString().Should().Be("Estado"); // Columna AF
        worksheet.Cell(1, 33).GetString().Should().Be("Fecha Registro UTC"); // Columna AG
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
        worksheet.Cell(2, 1).GetDouble().Should().Be(101);
        worksheet.Cell(2, 4).GetString().Should().Be("JUAN VALDEZ");
        worksheet.Cell(2, 5).GetString().Should().Be("WCN410");

        // Columna PEAJES (J = 10)
        worksheet.Cell(2, 10).GetDouble().Should().Be(1500000);
        // Columna PARQUEO (M = 13)
        worksheet.Cell(2, 13).GetDouble().Should().Be(178000);

        // ACPM (N = 14) debe ser 0
        worksheet.Cell(2, 14).GetDouble().Should().Be(0);

        // Total Gastos (Z = 26), Flete (AA = 27), Anticipo (AB = 28), Saldo Viaje (AC = 29)
        worksheet.Cell(2, 26).GetDouble().Should().Be(1678000);
        worksheet.Cell(2, 27).GetDouble().Should().Be(6976800);
        worksheet.Cell(2, 28).GetDouble().Should().Be(2800000);
        worksheet.Cell(2, 29).GetDouble().Should().Be(-1122000);
    }
}
