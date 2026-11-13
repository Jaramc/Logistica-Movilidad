using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.Interfaces;

namespace LogisticaBackend.Services;

/// <summary>
/// Servicio de exportación a Excel en streaming que evita asignación en el Large Object Heap (LOH).
/// Mapea los gastos dinámicos 1:N a las 16 columnas fijas canónicas del informe corporativo.
/// </summary>
public class ExcelExportService : IExcelExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ExcelExportService> _logger;

    public ExcelExportService(AppDbContext context, ILogger<ExcelExportService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MemoryStream> GenerarConsolidadoExcelAsync(
        DateOnly? desde = null,
        DateOnly? hasta = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Consultando base de datos para generar consolidado Excel (Filtros: Desde={Desde}, Hasta={Hasta}).", desde, hasta);

        var query = _context.Planillas
            .AsNoTracking()
            .Include(p => p.Gastos)
            .AsQueryable();

        if (desde.HasValue)
        {
            query = query.Where(p => p.FechaInicial >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(p => p.FechaFinal <= hasta.Value);
        }

        var planillas = await query
            .OrderByDescending(p => p.FechaInicial)
            .Take(1000) // Límite de seguridad
            .ToListAsync(cancellationToken);

        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Consolidado Planillas");

        // Encabezados corporativos (A a AG: 33 columnas)
        string[] headers = new[]
        {
            "ID", "Fecha Inicial", "Fecha Final", "Conductor", "Placa", "Manifiesto",
            "Empresa Transportadora", "Origen", "Destino",
            // Las 16 columnas fijas canónicas de gastos (J a Y)
            "Peajes", "Porcentaje", "Comisión", "Parqueo", "ACPM", "Engrase", "Tensionada",
            "Aseo", "Llantas", "Urea", "Cargue", "Descargue", "Carrosada", "Descarrosada",
            "Papelería", "Otros",
            // Totales y Balances (Z a AF)
            "Total Gastos", "Flete", "Anticipo", "Saldo Viaje", "Saldo Anterior", "Saldo Total",
            "Estado", "Fecha Registro UTC"
        };

        // Estilos de cabecera
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 2;
        foreach (var p in planillas)
        {
            worksheet.Cell(row, 1).Value = p.Id;
            worksheet.Cell(row, 2).Value = p.FechaInicial.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 3).Value = p.FechaFinal.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 4).Value = p.Conductor;
            worksheet.Cell(row, 5).Value = p.Placa;
            worksheet.Cell(row, 6).Value = p.Manifiesto ?? string.Empty;
            worksheet.Cell(row, 7).Value = p.EmpresaTransportadora ?? string.Empty;
            worksheet.Cell(row, 8).Value = p.Origen ?? string.Empty;
            worksheet.Cell(row, 9).Value = p.Destino ?? string.Empty;

            // Agrupación de gastos dinámicos por categoría canónica (Cols 10 a 25)
            var gastosPorCat = p.Gastos
                .GroupBy(g => ConceptoNormalizer.Normalizar(g.ConceptoCanonico ?? g.Concepto))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Valor), StringComparer.OrdinalIgnoreCase);

            for (int colCat = 0; colCat < ConceptoNormalizer.CategoriasCanonicas.Count; colCat++)
            {
                var categoria = ConceptoNormalizer.CategoriasCanonicas[colCat];
                decimal valorCat = gastosPorCat.TryGetValue(categoria, out var suma) ? suma : 0m;
                worksheet.Cell(row, 10 + colCat).Value = (double)valorCat;
            }

            // Totales financieros y balances
            worksheet.Cell(row, 26).Value = (double)p.TotalGastos;
            worksheet.Cell(row, 27).Value = (double)p.Flete;
            worksheet.Cell(row, 28).Value = (double)p.Anticipo;
            worksheet.Cell(row, 29).Value = (double)p.SaldoViaje;
            worksheet.Cell(row, 30).Value = (double)p.SaldoAnterior;
            worksheet.Cell(row, 31).Value = (double)p.SaldoTotal;

            // Formato de moneda para columnas financieras (J a AF)
            for (int col = 10; col <= 31; col++)
            {
                worksheet.Cell(row, col).Style.NumberFormat.Format = "$ #,##0.00";
            }

            worksheet.Cell(row, 32).Value = p.EstadoRevision.ToString();
            worksheet.Cell(row, 33).Value = p.CreadoEn.ToString("yyyy-MM-dd HH:mm:ss");

            row++;
        }

        // Configuración de tabla: congelar cabecera, autofiltro y autoajuste
        worksheet.SheetView.FreezeRows(1);
        if (row > 2)
        {
            worksheet.Range(1, 1, row - 1, headers.Length).SetAutoFilter();
        }
        worksheet.Columns().AdjustToContents();

        // Streaming directo a MemoryStream con Position = 0 (Sin ToArray)
        var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        memoryStream.Position = 0;

        return memoryStream;
    }
}

