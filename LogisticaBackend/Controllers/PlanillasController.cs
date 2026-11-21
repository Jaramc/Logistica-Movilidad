using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LogisticaBackend.Auth;
using LogisticaBackend.Configuration;
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
    private readonly LiquidacionesOptions _reglas;
    private readonly ILogger<PlanillasController> _logger;

    public PlanillasController(
        IPlanillaOcrService ocrService,
        IExcelExportService excelService,
        AppDbContext context,
        TimeProvider reloj,
        IAuditoriaService auditoria,
        IOptions<LiquidacionesOptions> reglas,
        ILogger<PlanillasController> logger)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
        _reglas = reglas?.Value ?? throw new ArgumentNullException(nameof(reglas));
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
        var conductor = NormalizarConductor(dto.Conductor);

        // Transacción + bloqueo por conductor: dos guardados simultáneos del mismo conductor no
        // pueden tomar el mismo saldo anterior.
        await using var transaccion = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (_context.Database.IsNpgsql())
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({"planilla-conductor:" + conductor}))", cancellationToken);
        }

        // El saldo anterior NUNCA viene del cliente: es el SaldoTotal de la última planilla del conductor.
        var (saldoAnterior, _) = await SaldoAnteriorAsync(conductor, cancellationToken);

        var calculo = CalculadoraLiquidacion.Calcular(
            dto.Flete, dto.Anticipo, dto.Gastos.Select(g => g.Valor), saldoAnterior);

        var erroresSaldo = ValidarTopeSaldos(saldoAnterior, calculo);
        if (erroresSaldo.Count > 0)
        {
            return Problemas.Validacion(HttpContext, erroresSaldo);
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;

        var (jsonOcr, resultadoJson) = JsonOcrCrudo.Sanear(dto.JsonOcrCrudo);
        if (resultadoJson is JsonOcrCrudo.Resultado.DemasiadoGrande or JsonOcrCrudo.Resultado.Invalido)
        {
            _logger.LogWarning("jsonOcrCrudo descartado ({Motivo}); se guarda null. Usuario {UsuarioId}.", resultadoJson, usuarioId);
        }

        // Mapeo explícito: solo se copian los campos permitidos (anti-overposting).
        var entidad = new PlanillaViaje
        {
            Placa = Placa.Normalizar(dto.Placa),
            Conductor = conductor,
            Origen = dto.Origen.Trim(),
            Destino = dto.Destino.Trim(),
            FechaInicial = dto.FechaInicial,
            FechaFinal = dto.FechaFinal,
            Manifiesto = string.IsNullOrWhiteSpace(dto.Manifiesto) ? null : dto.Manifiesto.Trim(),
            EmpresaTransportadora = string.IsNullOrWhiteSpace(dto.EmpresaTransportadora) ? null : dto.EmpresaTransportadora.Trim(),
            Flete = CalculadoraLiquidacion.Redondear(dto.Flete),
            Anticipo = CalculadoraLiquidacion.Redondear(dto.Anticipo),
            SaldoAnterior = CalculadoraLiquidacion.Redondear(saldoAnterior),
            TotalGastos = calculo.TotalGastos,
            SaldoViaje = calculo.SaldoViaje,
            SaldoTotal = calculo.SaldoTotal,
            EstadoRevision = EstadoRevision.Aprobado,
            JsonOcrCrudo = jsonOcr,
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
        if (transaccion is not null)
        {
            await transaccion.CommitAsync(cancellationToken);
        }

        _logger.LogInformation("Planilla {Id} guardada por el usuario {UsuarioId}.", entidad.Id, usuarioId);
        var advertencias = new List<string>();
        if (dto.SaldoAnteriorDeclarado is { } declarado && CalculadoraLiquidacion.Redondear(declarado) != entidad.SaldoAnterior)
        {
            advertencias.Add($"El saldo anterior escrito en la planilla ({DescribirSaldo(declarado)}) difiere del calculado " +
                $"desde la última planilla del conductor ({DescribirSaldo(entidad.SaldoAnterior)}). Se guardó el calculado.");
        }
        await _auditoria.RegistrarAsync(AccionesAuditoria.PlanillaGuardada, true, usuarioId, "PlanillaViaje", entidad.Id.ToString(),
            new { placa = entidad.Placa, renglones = entidad.Gastos.Count, entidad.TotalGastos, entidad.SaldoTotal });

        return CreatedAtAction(
            nameof(ObtenerPlanillaPorId),
            new { id = entidad.Id },
            new PlanillaGuardadaDto("Planilla aprobada y guardada.", entidad.Id, ADetalle(entidad),
                advertencias.Count > 0 ? advertencias : null));
    }

    /// <summary>
    /// Saldo anterior que tendrá la próxima planilla del conductor (el SaldoTotal de su última planilla).
    /// La UI lo muestra antes de aprobar para compararlo con el escrito en papel.
    /// </summary>
    [HttpGet("saldo-anterior")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(SaldoAnteriorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObtenerSaldoAnterior([FromQuery] string? conductor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conductor) || conductor.Length > 150)
        {
            return Problemas.Validacion(HttpContext, new Dictionary<string, string[]>
            {
                ["conductor"] = new[] { "Indique el conductor (máximo 150 caracteres)." },
            });
        }

        var normalizado = NormalizarConductor(conductor);
        var (saldo, planillaId) = await SaldoAnteriorAsync(normalizado, cancellationToken);
        return Ok(new SaldoAnteriorDto(normalizado, saldo, planillaId));
    }

    private async Task<(decimal Saldo, int? PlanillaId)> SaldoAnteriorAsync(string conductorNormalizado, CancellationToken cancellationToken)
    {
        var ultima = await _context.Planillas.AsNoTracking()
            .Where(p => p.Conductor.ToUpper() == conductorNormalizado)
            .OrderByDescending(p => p.FechaFinal).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.SaldoTotal })
            .FirstOrDefaultAsync(cancellationToken);
        return ultima is null ? (0m, null) : (ultima.SaldoTotal, ultima.Id);
    }

    /// <summary>"$63.500 a favor de la empresa": positivo = a favor del conductor.</summary>
    private static string DescribirSaldo(decimal saldo)
    {
        var texto = "$" + Math.Abs(saldo).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        return saldo switch
        {
            > 0m => texto + " a favor del conductor",
            < 0m => texto + " a favor de la empresa",
            _ => texto,
        };
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

    /// <summary>Mayúsculas y espacios simples: "  pedro  perez " y "PEDRO PEREZ" son el mismo conductor.</summary>
    public static string NormalizarConductor(string conductor) =>
        string.Join(' ', conductor.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    private Dictionary<string, string[]> ValidarTopeSaldos(decimal saldoAnterior, ResultadoCalculo calculo)
    {
        var tope = _reglas.TopeSaldo;
        var errores = new Dictionary<string, string[]>();
        var texto = tope.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        void Revisar(string campo, string nombre, decimal valor)
        {
            if (Math.Abs(valor) > tope)
            {
                errores[campo] = new[] { $"El {nombre} calculado ({valor:N0}) supera el máximo permitido de ±{texto}." };
            }
        }
        Revisar("saldoAnterior", "saldo anterior", saldoAnterior);
        Revisar("saldoViaje", "saldo del viaje", calculo.SaldoViaje);
        Revisar("saldoTotal", "saldo total", calculo.SaldoTotal);
        return errores;
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
