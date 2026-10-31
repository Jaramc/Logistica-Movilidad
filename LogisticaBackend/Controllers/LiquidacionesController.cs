using Microsoft.AspNetCore.Mvc;
using TuProyecto.Application.Interfaces;
using LogisticaBackend.Data;
using LogisticaBackend.Models;
using Microsoft.EntityFrameworkCore;

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
}
