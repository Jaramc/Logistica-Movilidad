using Microsoft.AspNetCore.Mvc;
using TuProyecto.Application.Interfaces;
using LogisticaBackend.Data;
using LogisticaBackend.Models;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace TuProyecto.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LiquidacionesController : ControllerBase
{
    private readonly IOcrAzureService _ocrService;
    private readonly AppDbContext? _context;
    private readonly ILogger<LiquidacionesController> _logger;

    // Inyectamos el servicio de Azure y el contexto de base de datos
    public LiquidacionesController(
        IOcrAzureService ocrService, 
        ILogger<LiquidacionesController> logger,
        AppDbContext? context = null)
    {
        _ocrService = ocrService;
        _logger = logger;
        _context = context;
    }

    [HttpPost("extraer-datos")]
    public async Task<IActionResult> ExtraerDatosLiquidacion(IFormFile imagen)
    {
        // 1. Validaciones básicas
        if (imagen == null || imagen.Length == 0)
        {
            return BadRequest(new { mensaje = "No se proporcionó ninguna imagen." });
        }

        var extensionesValidas = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        var extension = Path.GetExtension(imagen.FileName).ToLower();
        
        if (!extensionesValidas.Contains(extension))
        {
            return BadRequest(new { mensaje = "Formato de archivo no soportado. Usa JPG, PNG o PDF." });
        }

        try
        {
            // 2. Procesamos el archivo sin guardarlo físicamente en el disco
            using var stream = imagen.OpenReadStream();
            
            // Usamos "prebuilt-receipt" para los tiquetes, o el ID de tu modelo personalizado para la planilla
            string modelId = "prebuilt-receipt"; 
            
            var resultado = await _ocrService.ProcesarDocumentoAsync(stream, modelId);

            // 3. Devolvemos los datos al frontend (React) para que la encargada los revise
            return Ok(new 
            { 
                mensaje = "Datos extraídos correctamente", 
                datos = resultado 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar la imagen con Azure.");
            return StatusCode(500, new { mensaje = "Error al procesar la imagen con Azure", error = ex.Message });
        }
    }

    [HttpPost("guardar")]
    public async Task<IActionResult> GuardarLiquidacion([FromBody] LiquidacionViaje liquidacion)
    {
        if (liquidacion == null)
        {
            return BadRequest(new { mensaje = "Los datos de la liquidación no son válidos." });
        }

        try
        {
            liquidacion.EstadoRevision = "aprobado";
            liquidacion.CreadoEn = DateTime.UtcNow;
            liquidacion.ActualizadoEn = DateTime.UtcNow;

            if (_context != null)
            {
                await _context.LiquidacionesViaje.AddAsync(liquidacion);
                await _context.SaveChangesAsync();
                return Ok(new 
                { 
                    mensaje = "Liquidación aprobada y guardada exitosamente en la base de datos.", 
                    id = liquidacion.Id 
                });
            }

            return Ok(new 
            { 
                mensaje = "Liquidación aprobada correctamente (DbContext no conectado).", 
                datos = liquidacion 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la liquidación.");
            return StatusCode(500, new { mensaje = "Error al guardar en PostgreSQL", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerLiquidaciones()
    {
        if (_context == null)
        {
            return Ok(new List<LiquidacionViaje>());
        }

        var lista = await _context.LiquidacionesViaje
            .OrderByDescending(x => x.CreadoEn)
            .Take(50)
            .ToListAsync();

        return Ok(lista);
    }

    [HttpGet("descargar-consolidado")]
    public async Task<IActionResult> DescargarConsolidado()
    {
        try
        {
            List<LiquidacionViaje> liquidaciones = new();
            if (_context != null)
            {
                liquidaciones = await _context.LiquidacionesViaje
                    .OrderByDescending(x => x.FechaInicial)
                    .ToListAsync();
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Consolidado Liquidaciones");

            // Encabezados de columnas
            string[] headers = new[]
            {
                "ID", "Fecha Inicial", "Fecha Final", "Conductor", "Placa", "Manifiesto", 
                "Empresa Transportadora", "Origen", "Destino",
                "Peajes", "Porcentaje", "Comisión", "Parqueo", "ACPM", "Engrase", "Tensionada",
                "Aseo", "Llantas", "Urea", "Cargue", "Descargue", "Carrosada", "Descarrosada",
                "Papelería", "Otros",
                "Total Gastos", "Traslado", "Básico", "Saldo Viaje", "Saldo Anterior", "Saldo Total",
                "Estado", "Fecha Registro"
            };

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
            foreach (var l in liquidaciones)
            {
                worksheet.Cell(row, 1).Value = l.Id;
                worksheet.Cell(row, 2).Value = l.FechaInicial.ToString("yyyy-MM-dd");
                worksheet.Cell(row, 3).Value = l.FechaFinal.ToString("yyyy-MM-dd");
                worksheet.Cell(row, 4).Value = l.Conductor;
                worksheet.Cell(row, 5).Value = l.Placa;
                worksheet.Cell(row, 6).Value = l.Manifiesto ?? "";
                worksheet.Cell(row, 7).Value = l.EmpresaTransportadora ?? "";
                worksheet.Cell(row, 8).Value = l.OrigenViaje ?? "";
                worksheet.Cell(row, 9).Value = l.Destino ?? "";

                worksheet.Cell(row, 10).Value = (double)l.Peajes;
                worksheet.Cell(row, 11).Value = (double)l.Porcentaje;
                worksheet.Cell(row, 12).Value = (double)l.Comision;
                worksheet.Cell(row, 13).Value = (double)l.Parqueo;
                worksheet.Cell(row, 14).Value = (double)l.Acpm;
                worksheet.Cell(row, 15).Value = (double)l.Engrase;
                worksheet.Cell(row, 16).Value = (double)l.Tensionada;
                worksheet.Cell(row, 17).Value = (double)l.Aseo;
                worksheet.Cell(row, 18).Value = (double)l.Llantas;
                worksheet.Cell(row, 19).Value = (double)l.Urea;
                worksheet.Cell(row, 20).Value = (double)l.Cargue;
                worksheet.Cell(row, 21).Value = (double)l.Descargue;
                worksheet.Cell(row, 22).Value = (double)l.Carrosada;
                worksheet.Cell(row, 23).Value = (double)l.Descarrosada;
                worksheet.Cell(row, 24).Value = (double)l.Papeleria;
                worksheet.Cell(row, 25).Value = (double)l.Otros;

                worksheet.Cell(row, 26).Value = (double)l.TotalGastos;
                worksheet.Cell(row, 27).Value = (double)l.Traslado;
                worksheet.Cell(row, 28).Value = (double)l.Basico;
                worksheet.Cell(row, 29).Value = (double)l.SaldoViaje;
                worksheet.Cell(row, 30).Value = (double)l.SaldoAnterior;
                worksheet.Cell(row, 31).Value = (double)l.SaldoTotal;

                for (int col = 10; col <= 31; col++)
                {
                    worksheet.Cell(row, col).Style.NumberFormat.Format = "$ #,##0";
                }

                worksheet.Cell(row, 32).Value = l.EstadoRevision;
                worksheet.Cell(row, 33).Value = l.CreadoEn.ToString("yyyy-MM-dd HH:mm");
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            var fileContent = memoryStream.ToArray();

            return File(
                fileContent,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Liquidaciones_Consolidadas_{DateTime.Now:yyyyMMdd}.xlsx"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar el consolidado de Excel.");
            return StatusCode(500, new { mensaje = "Error al exportar consolidado", error = ex.Message });
        }
    }
}
