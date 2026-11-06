using Azure;
using Microsoft.AspNetCore.Mvc;
using LogisticaBackend.Interfaces;
using LogisticaBackend.DTOs;
// using LogisticaBackend.Data; // Descomentar al integrar LogisticaDbContext

namespace LogisticaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanillaController : ControllerBase
{
    private readonly IPlanillaOcrService _ocrService;
    private readonly ILogger<PlanillaController> _logger;
    // private readonly LogisticaDbContext _context; // Descomentar al habilitar la persistencia en base de datos

    public PlanillaController(
        IPlanillaOcrService ocrService,
        ILogger<PlanillaController> logger
        /* , LogisticaDbContext context */)
    {
        _ocrService = ocrService;
        _logger = logger;
        // _context = context;
    }

    [HttpPost("procesar-ocr")]
    public async Task<IActionResult> ProcesarOcr(IFormFile? file)
    {
        // 1. Validación de presencia y tamaño básico
        if (file == null || file.Length == 0)
        {
            return BadRequest(new 
            { 
                mensaje = "No se ha proporcionado ningún archivo o el archivo está vacío." 
            });
        }

        // 2. Validación de ContentType permitido
        var allowedMimeTypes = new[]
        {
            "application/pdf",
            "image/jpeg",
            "image/png",
            "image/tiff"
        };

        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!allowedMimeTypes.Contains(contentType))
        {
            return BadRequest(new 
            { 
                mensaje = "Formato de archivo no soportado. Únicamente se permiten archivos: PDF, JPEG, PNG o TIFF." 
            });
        }

        try
        {
            // 3. Procesamiento en memoria como Stream sin almacenamiento en disco
            using var stream = file.OpenReadStream();
            PlanillaOcrResultDto resultado = await _ocrService.ProcesarPlanillaAsync(stream);

            // 4. Mapeo y Persistencia en PostgreSQL mediante Entity Framework Core (Comentado para futura integración)
            /*
            var planillaEntidad = new PlanillaViaje
            {
                Placa = resultado.Placa ?? string.Empty,
                Origen = resultado.Origen,
                Destino = resultado.Destino,
                Flete = (decimal)resultado.Flete,
                Anticipo = (decimal)resultado.Anticipo,
                TotalGastos = (decimal)resultado.TotalGastos,
                SaldoViaje = (decimal)resultado.SaldoViaje,
                FechaRegistro = DateTime.UtcNow,
                GastosDetalle = resultado.DetalleGastos.Select(g => new GastoDetalleEntidad
                {
                    Concepto = g.Concepto,
                    Valor = (decimal)g.Valor
                }).ToList()
            };

            await _context.Planillas.AddAsync(planillaEntidad);
            await _context.SaveChangesAsync();
            */

            // 5. Respuesta exitosa con la información estructurada
            return Ok(new
            {
                mensaje = "Planilla procesada exitosamente.",
                data = resultado
            });
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Fallo al comunicar con Azure Document Intelligence. Código: {ErrorCode}, Status: {Status}", ex.ErrorCode, ex.Status);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                mensaje = "Error de comunicación con el servicio de Azure AI Document Intelligence.",
                detalle = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocurrió un error inesperado al procesar la planilla física.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                mensaje = "Ocurrió un error interno en el servidor al procesar la planilla.",
                detalle = ex.Message
            });
        }
    }
}

