using ClosedXML.Excel;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace LogisticaBackend.Tests;

public class ExcelInspector
{
    private readonly ITestOutputHelper _output;

    public ExcelInspector(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspeccionarExcelReal()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "liquidaciones_de_gastos_2025.xlsx");
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "liquidaciones_de_gastos_2025.xlsx");
        }
        if (!File.Exists(path))
        {
            path = @"C:\Users\ximen\OneDrive\Desktop\Logistica-Movilidad\liquidaciones_de_gastos_2025.xlsx";
        }

        _output.WriteLine($"Leyendo Excel desde: {path}");
        using var workbook = new XLWorkbook(path);
        foreach (var ws in workbook.Worksheets)
        {
            _output.WriteLine($"Hoja: '{ws.Name}', filas usadas: {ws.RowCount()}, columnas: {ws.ColumnCount()}");
        }

        var sheet = workbook.Worksheets.First();
        _output.WriteLine("--- ENCABEZADOS FILA 1 ---");
        for (int col = 1; col <= 35; col++)
        {
            var cell = sheet.Cell(1, col);
            _output.WriteLine($"Col {col} ({XLHelper.GetColumnLetterFromNumber(col)}): '{cell.GetString()}'");
        }

        _output.WriteLine("--- PRIMERAS 15 FILAS ---");
        for (int row = 2; row <= 15; row++)
        {
            var rowVals = new List<string>();
            for (int col = 1; col <= 30; col++)
            {
                var cell = sheet.Cell(row, col);
                var f = cell.HasFormula ? $"[={cell.FormulaA1}]" : "";
                var v = cell.Value.ToString();
                if (!string.IsNullOrEmpty(v))
                {
                    rowVals.Add($"{XLHelper.GetColumnLetterFromNumber(col)}:{v}{f}");
                }
            }
            _output.WriteLine($"Fila {row}: {string.Join(" | ", rowVals)}");
        }
    }
}

