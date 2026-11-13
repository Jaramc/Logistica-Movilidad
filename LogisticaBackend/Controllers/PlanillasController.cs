using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Services;

namespace LogisticaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PlanillasController : ControllerBase
{
    private readonly IPlanillaOcrService _ocrService;
    private readonly IExcelExportService _excelService;
    private readonly AppDbContext? _context;
    private readonly IValidator<GuardarPlanillaDto> _validator;
    private readonly ILogger<PlanillasController> _logger;

    public PlanillasController(
        IPlanillaOcrService ocrService,
        IExcelExportService excelService,
        IValidator<GuardarPlanillaDto> validator,
        ILogger<PlanillasController> logger,
        AppDbContext? context = null)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context; // Desacoplado: permite extracción OCR sin base de datos activa
    }

    /// <summary>
    /// Extrae información estructurada de una planilla de transporte físico mediante Azure AI Document Intelligence.
    /// No depende de la disponibilidad de la base de datos PostgreSQL.
    /// </summary>
    [HttpPost("extraer")]
    [EnableRateLimiting("ocr_policy")]
    [RequestSizeLimit(FileValidationService.TamanoMaximoBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PlanillaOcrResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ExtraerDatos(IFormFile? file, CancellationToken cancellationToken)
    {
        // 1. Validación de tamaño y Magic Bytes en memoria
        var validacion = await FileValidationService.ValidarArchivoAsync(file, cancellationToken);
        if (!validacion.EsValido)
        {
            var status = file != null && file.Length > FileValidationService.TamanoMaximoBytes
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status415UnsupportedMediaType;

            return StatusCode(status, new ProblemDetails
            {
                Status = status,
                Title = "Archivo no admitido",
                Detail = validacion.MensajeError,
                Instance = HttpContext.Request.Path
            });
        }

        // 2. Procesamiento OCR en memoria como Stream sin almacenamiento en disco
        using var stream = file!.OpenReadStream();
        var resultado = await _ocrService.ProcesarPlanillaAsync(stream, cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Aprueba y persiste la liquidación definitiva en PostgreSQL con relación 1:N a GastoDetalle.
    /// Recalcula autoritativamente los totales y rechaza inconsistencias mayores a 0.01.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GuardarPlanilla(
        [FromBody] GuardarPlanillaDto dto,
        CancellationToken cancellationToken)
    {
        if (dto == null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Payload Inválido",
                Detail = "No se recibieron datos de la planilla.",
                Instance = HttpContext.Request.Path
            });
        }

        // 1. Validación de reglas de entrada con FluentValidation
        var validacionFluent = await _validator.ValidateAsync(dto, cancellationToken);
        if (!validacionFluent.IsValid)
        {
            var errors = validacionFluent.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return StatusCode(StatusCodes.Status422UnprocessableEntity, new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Error de Validación de Formulario",
                Detail = "Uno o más campos no cumplen con las reglas del negocio.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["errors"] = errors }
            });
        }

        // 2. Validación autoritativa de totales y saldos (Zona Crítica Contable)
        var (esValido, mensajeError, esperado) = CalculadoraLiquidacion.ValidarTotalesEnviados(dto);
        if (!esValido)
        {
            _logger.LogWarning("Rechazo 422 por discrepancia contable. {Detalle}", mensajeError);
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Discrepancia en Totales Contables",
                Detail = mensajeError,
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["valoresEsperados"] = new
                    {
                        totalGastos = esperado.TotalGastos,
                        saldoViaje = esperado.SaldoViaje,
                        saldoTotal = esperado.SaldoTotal
                    },
                    ["valoresRecibidos"] = new
                    {
                        totalGastos = dto.TotalGastos,
                        saldoViaje = dto.SaldoViaje,
                        saldoTotal = dto.SaldoTotal
                    }
                }
            });
        }

        // 3. Verificación de disponibilidad de base de datos
        if (_context == null || !await VerificarBaseDatosDisponibleAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Base de Datos No Disponible",
                Detail = "El servicio de persistencia PostgreSQL no está conectado actualmente. Por favor revise 'DefaultConnection'.",
                Instance = HttpContext.Request.Path
            });
        }

        // 4. Mapeo explícito a entidad de dominio (Anti-Overposting)
        var entidad = new PlanillaViaje
        {
            Placa = dto.Placa.Trim().ToUpperInvariant(),
            Conductor = dto.Conductor.Trim(),
            Origen = dto.Origen.Trim(),
            Destino = dto.Destino.Trim(),
            FechaInicial = dto.FechaInicial,
            FechaFinal = dto.FechaFinal,
            Manifiesto = string.IsNullOrWhiteSpace(dto.Manifiesto) ? null : dto.Manifiesto.Trim(),
            EmpresaTransportadora = string.IsNullOrWhiteSpace(dto.EmpresaTransportadora) ? null : dto.EmpresaTransportadora.Trim(),
            Flete = CalculadoraLiquidacion.Redondear(dto.Flete),
            Anticipo = CalculadoraLiquidacion.Redondear(dto.Anticipo),
            TotalGastos = esperado.TotalGastos,
            SaldoViaje = esperado.SaldoViaje,
            SaldoAnterior = CalculadoraLiquidacion.Redondear(dto.SaldoAnterior),
            SaldoTotal = esperado.SaldoTotal,
            EstadoRevision = EstadoRevision.Aprobado,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow,
            Gastos = dto.Gastos.Select((g, idx) => new GastoDetalle
            {
                Concepto = g.Concepto.Trim(),
                ConceptoCanonico = ConceptoNormalizer.Normalizar(g.Concepto),
                Valor = CalculadoraLiquidacion.Redondear(g.Valor),
                Orden = idx + 1
            }).ToList()
        };

        await _context.Planillas.AddAsync(entidad, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Planilla #{Id} aprobada y persistida exitosamente para el vehículo {Placa}.", entidad.Id, entidad.Placa);

        var respuestaData = new PlanillaDetalleDto(
            Id: entidad.Id,
            Placa: entidad.Placa,
            Conductor: entidad.Conductor,
            Origen: entidad.Origen,
            Destino: entidad.Destino,
            FechaInicial: entidad.FechaInicial,
            FechaFinal: entidad.FechaFinal,
            Manifiesto: entidad.Manifiesto,
            EmpresaTransportadora: entidad.EmpresaTransportadora,
            Flete: entidad.Flete,
            Anticipo: entidad.Anticipo,
            TotalGastos: entidad.TotalGastos,
            SaldoViaje: entidad.SaldoViaje,
            SaldoAnterior: entidad.SaldoAnterior,
            SaldoTotal: entidad.SaldoTotal,
            EstadoRevision: entidad.EstadoRevision.ToString(),
            CreadoEn: entidad.CreadoEn,
            ActualizadoEn: entidad.ActualizadoEn,
            Gastos: entidad.Gastos.Select(g => new GastoDetalleRespuestaDto(
                Id: g.Id,
                Concepto: g.Concepto,
                ConceptoCanonico: g.ConceptoCanonico,
                Valor: g.Valor,
                Orden: g.Orden
            )).ToList()
        );

        return CreatedAtAction(
            nameof(ObtenerPlanillaPorId),
            new { id = entidad.Id },
            new
            {
                mensaje = "Planilla aprobada y guardada exitosamente en la base de datos.",
                id = entidad.Id,
                data = respuestaData
            }
        );
    }

    /// <summary>
    /// Consulta el historial paginado de planillas aprobadas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PlanillaResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObtenerHistorial(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (_context == null || !await VerificarBaseDatosDisponibleAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Base de Datos No Disponible",
                Detail = "No se puede consultar el historial porque PostgreSQL no está conectado.",
                Instance = HttpContext.Request.Path
            });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await _context.Planillas.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await _context.Planillas
            .AsNoTracking()
            .OrderByDescending(p => p.FechaInicial)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlanillaResumenDto(
                p.Id,
                p.Placa,
                p.Conductor,
                p.Origen,
                p.Destino,
                p.FechaInicial,
                p.FechaFinal,
                p.TotalGastos,
                p.SaldoViaje,
                p.SaldoTotal,
                p.EstadoRevision.ToString(),
                p.CreadoEn
            ))
            .ToListAsync(cancellationToken);

        var pagedResult = new PagedResultDto<PlanillaResumenDto>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );

        return Ok(new
        {
            mensaje = "Historial consultado exitosamente.",
            data = pagedResult
        });
    }

    /// <summary>
    /// Consulta el detalle individual de una planilla con sus renglones de gastos.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PlanillaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPlanillaPorId(int id, CancellationToken cancellationToken)
    {
        if (_context == null || !await VerificarBaseDatosDisponibleAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Base de Datos No Disponible",
                Detail = "PostgreSQL no está disponible.",
                Instance = HttpContext.Request.Path
            });
        }

        var planilla = await _context.Planillas
            .AsNoTracking()
            .Include(p => p.Gastos.OrderBy(g => g.Orden))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (planilla == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Planilla No Encontrada",
                Detail = $"No se encontró ninguna planilla con el identificador #{id}.",
                Instance = HttpContext.Request.Path
            });
        }

        var detalle = new PlanillaDetalleDto(
            Id: planilla.Id,
            Placa: planilla.Placa,
            Conductor: planilla.Conductor,
            Origen: planilla.Origen,
            Destino: planilla.Destino,
            FechaInicial: planilla.FechaInicial,
            FechaFinal: planilla.FechaFinal,
            Manifiesto: planilla.Manifiesto,
            EmpresaTransportadora: planilla.EmpresaTransportadora,
            Flete: planilla.Flete,
            Anticipo: planilla.Anticipo,
            TotalGastos: planilla.TotalGastos,
            SaldoViaje: planilla.SaldoViaje,
            SaldoAnterior: planilla.SaldoAnterior,
            SaldoTotal: planilla.SaldoTotal,
            EstadoRevision: planilla.EstadoRevision.ToString(),
            CreadoEn: planilla.CreadoEn,
            ActualizadoEn: planilla.ActualizadoEn,
            Gastos: planilla.Gastos.Select(g => new GastoDetalleRespuestaDto(
                Id: g.Id,
                Concepto: g.Concepto,
                ConceptoCanonico: g.ConceptoCanonico,
                Valor: g.Valor,
                Orden: g.Orden
            )).ToList()
        );

        return Ok(detalle);
    }

    /// <summary>
    /// Genera y descarga el consolidado en Excel en formato streaming (evitando LOH).
    /// </summary>
    [HttpGet("descargar-consolidado")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DescargarConsolidado(
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancellationToken)
    {
        if (_context == null || !await VerificarBaseDatosDisponibleAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Base de Datos No Disponible",
                Detail = "No se puede generar el archivo consolidado porque PostgreSQL no está conectado.",
                Instance = HttpContext.Request.Path
            });
        }

        var memoryStream = await _excelService.GenerarConsolidadoExcelAsync(desde, hasta, cancellationToken);

        return File(
            fileStream: memoryStream,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: $"Consolidado_Planillas_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx"
        );
    }

    /// <summary>
    /// Retorna el catálogo canónico de las 16 categorías fijas de gastos para autocompletado del frontend.
    /// </summary>
    [HttpGet("conceptos")]
    [ProducesResponseType(typeof(IReadOnlyList<ConceptoCanonicoDto>), StatusCodes.Status200OK)]
    public IActionResult ObtenerCatalogoConceptos()
    {
        return Ok(ConceptoNormalizer.ObtenerCatalogo());
    }

    private async Task<bool> VerificarBaseDatosDisponibleAsync(CancellationToken cancellationToken)
    {
        try
        {
            return _context != null && await _context.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }
}

