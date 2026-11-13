using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using LogisticaBackend.Interfaces;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;

namespace LogisticaBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PlanillasController : ControllerBase
{
    private readonly IPlanillaOcrService _ocrService;
    private readonly IExcelExportService _excelService;
    private readonly AppDbContext _context;
    private readonly TimeProvider _reloj;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<PlanillasController> _logger;

    public PlanillasController(
        IPlanillaOcrService ocrService,
        IExcelExportService excelService,
        AppDbContext context,
        TimeProvider reloj,
        IAuditoriaService auditoria,
        ILogger<PlanillasController> logger)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extrae los datos de una planilla física con Azure AI Document Intelligence.
    /// </summary>
    [HttpPost("extraer")]
    [Authorize(Policy = Politicas.PuedeRevisar)]
    [EnableRateLimiting(LimitesPeticiones.PoliticaOcr)]
    [RequestSizeLimit(FileValidationService.TamanoMaximoCuerpoSubida)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileValidationService.TamanoMaximoCuerpoSubida)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PlanillaOcrResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ExtraerDatos(IFormFile? file, CancellationToken cancellationToken)
    {
        // Tamaño, extensión, MIME y magic bytes, en memoria.
        var validacion = await FileValidationService.ValidarArchivoAsync(file, cancellationToken);
        if (!validacion.EsValido)
        {
            var status = file != null && file.Length > FileValidationService.TamanoMaximoBytes
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status415UnsupportedMediaType;

            return Problem(statusCode: status, title: "Archivo no admitido", detail: validacion.MensajeError);
        }

        using var stream = file!.OpenReadStream();
        var resultado = await _ocrService.ProcesarPlanillaAsync(stream, cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Aprueba y guarda la planilla. Los totales SIEMPRE los calcula el servidor con decimal;
    /// cualquier total enviado por el cliente se ignora. La autoría sale de la sesión.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.PuedeRevisar)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(PlanillaGuardadaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GuardarPlanilla(
        [FromBody] GuardarPlanillaDto dto,
        CancellationToken cancellationToken)
    {
        var usuarioId = IdUsuarioActual();
        var calculo = CalculadoraLiquidacion.Calcular(
            dto.Flete, dto.Anticipo, dto.Gastos.Select(g => g.Valor), dto.SaldoAnterior);
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        // Mapeo explícito: solo se copian los campos permitidos (anti-overposting).
        var entidad = new PlanillaViaje
        {
            Placa = Placa.Normalizar(dto.Placa),
            Conductor = dto.Conductor.Trim(),
            Origen = dto.Origen.Trim(),
            Destino = dto.Destino.Trim(),
            FechaInicial = dto.FechaInicial,
            FechaFinal = dto.FechaFinal,
            Manifiesto = string.IsNullOrWhiteSpace(dto.Manifiesto) ? null : dto.Manifiesto.Trim(),
            EmpresaTransportadora = string.IsNullOrWhiteSpace(dto.EmpresaTransportadora) ? null : dto.EmpresaTransportadora.Trim(),
            Flete = CalculadoraLiquidacion.Redondear(dto.Flete),
            Anticipo = CalculadoraLiquidacion.Redondear(dto.Anticipo),
            SaldoAnterior = CalculadoraLiquidacion.Redondear(dto.SaldoAnterior),
            TotalGastos = calculo.TotalGastos,
            SaldoViaje = calculo.SaldoViaje,
            SaldoTotal = calculo.SaldoTotal,
            EstadoRevision = EstadoRevision.Aprobado,
            CreadoPorUsuarioId = usuarioId,
            RevisadoPorUsuarioId = usuarioId,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
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

        _logger.LogInformation("Planilla {Id} guardada por el usuario {UsuarioId}.", entidad.Id, usuarioId);
        await _auditoria.RegistrarAsync(AccionesAuditoria.PlanillaGuardada, true, usuarioId, "PlanillaViaje", entidad.Id.ToString(),
            new { placa = entidad.Placa, renglones = entidad.Gastos.Count, entidad.TotalGastos, entidad.SaldoTotal });

        return CreatedAtAction(
            nameof(ObtenerPlanillaPorId),
            new { id = entidad.Id },
            new PlanillaGuardadaDto("Planilla aprobada y guardada.", entidad.Id, ADetalle(entidad)));
    }

    /// <summary>Historial paginado (máximo 100 por página) con filtros por fechas, placa y conductor.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(PagedResultDto<PlanillaResumenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerHistorial(
        [FromQuery] ConsultaPlanillasDto consulta,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Planillas.AsNoTracking();

        if (consulta.Desde is { } desde) query = query.Where(p => p.FechaInicial >= desde);
        if (consulta.Hasta is { } hasta) query = query.Where(p => p.FechaInicial <= hasta);
        if (!string.IsNullOrWhiteSpace(consulta.Placa))
        {
            var placa = Placa.Normalizar(consulta.Placa);
            query = query.Where(p => p.Placa.StartsWith(placa));
        }
        if (!string.IsNullOrWhiteSpace(consulta.Conductor))
        {
            var conductor = consulta.Conductor.Trim().ToUpper();
            query = query.Where(p => p.Conductor.ToUpper().Contains(conductor));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)consulta.Tamano);

        var items = await query
            .OrderByDescending(p => p.FechaInicial)
            .ThenByDescending(p => p.Id)
            .Skip((consulta.Pagina - 1) * consulta.Tamano)
            .Take(consulta.Tamano)
            .Select(p => new PlanillaResumenDto(
                p.Id, p.Placa, p.Conductor, p.Origen, p.Destino, p.FechaInicial, p.FechaFinal,
                p.TotalGastos, p.SaldoViaje, p.SaldoTotal, p.EstadoRevision.ToString(), p.CreadoEn))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<PlanillaResumenDto>(items, totalCount, consulta.Pagina, consulta.Tamano, totalPages));
    }

    /// <summary>Detalle de una planilla con sus renglones de gasto.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(PlanillaDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPlanillaPorId(int id, CancellationToken cancellationToken)
    {
        var planilla = await _context.Planillas
            .AsNoTracking()
            .Include(p => p.Gastos.OrderBy(g => g.Orden))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (planilla == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Planilla no encontrada",
                detail: "No existe una planilla con ese identificador.");
        }

        return Ok(ADetalle(planilla));
    }

    /// <summary>Consolidado en Excel para un rango obligatorio de máximo 1 año.</summary>
    [HttpGet("descargar-consolidado")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DescargarConsolidado(
        [FromQuery] RangoFechasDescargaDto rango,
        CancellationToken cancellationToken)
    {
        var memoryStream = await _excelService.GenerarConsolidadoExcelAsync(rango.Desde, rango.Hasta, cancellationToken);
        await _auditoria.RegistrarAsync(AccionesAuditoria.ConsolidadoDescargado, true, IdUsuarioActual(), "PlanillaViaje", null,
            new { desde = rango.Desde, hasta = rango.Hasta, bytes = memoryStream.Length });

        return File(
            fileStream: memoryStream,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: $"Consolidado_Planillas_{rango.Desde:yyyyMMdd}_{rango.Hasta:yyyyMMdd}.xlsx"
        );
    }

    /// <summary>Catálogo de las 16 categorías canónicas de gasto.</summary>
    [HttpGet("conceptos")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(IReadOnlyList<ConceptoCanonicoDto>), StatusCodes.Status200OK)]
    public IActionResult ObtenerCatalogoConceptos()
    {
        return Ok(ConceptoNormalizer.ObtenerCatalogo());
    }

    private Guid? IdUsuarioActual()
    {
        var valor = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private static PlanillaDetalleDto ADetalle(PlanillaViaje p) => new(
        Id: p.Id,
        Placa: p.Placa,
        Conductor: p.Conductor,
        Origen: p.Origen,
        Destino: p.Destino,
        FechaInicial: p.FechaInicial,
        FechaFinal: p.FechaFinal,
        Manifiesto: p.Manifiesto,
        EmpresaTransportadora: p.EmpresaTransportadora,
        Flete: p.Flete,
        Anticipo: p.Anticipo,
        TotalGastos: p.TotalGastos,
        SaldoViaje: p.SaldoViaje,
        SaldoAnterior: p.SaldoAnterior,
        SaldoTotal: p.SaldoTotal,
        EstadoRevision: p.EstadoRevision.ToString(),
        CreadoEn: p.CreadoEn,
        ActualizadoEn: p.ActualizadoEn,
        CreadoPorUsuarioId: p.CreadoPorUsuarioId,
        RevisadoPorUsuarioId: p.RevisadoPorUsuarioId,
        Gastos: p.Gastos.OrderBy(g => g.Orden).Select(g => new GastoDetalleRespuestaDto(
            Id: g.Id,
            Concepto: g.Concepto,
            ConceptoCanonico: g.ConceptoCanonico,
            Valor: g.Valor,
            Orden: g.Orden
        )).ToList()
    );
}
