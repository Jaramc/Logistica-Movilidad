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
    private readonly ICuentaConductorService _cuenta;
    private readonly LiquidacionesOptions _reglas;
    private readonly ILogger<PlanillasController> _logger;

    public PlanillasController(
        IPlanillaOcrService ocrService,
        IExcelExportService excelService,
        AppDbContext context,
        TimeProvider reloj,
        IAuditoriaService auditoria,
        ICuentaConductorService cuenta,
        IOptions<LiquidacionesOptions> reglas,
        ILogger<PlanillasController> logger)
    {
        _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
        _excelService = excelService ?? throw new ArgumentNullException(nameof(excelService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
        _cuenta = cuenta ?? throw new ArgumentNullException(nameof(cuenta));
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarPlanilla(
        [FromBody] GuardarPlanillaDto dto,
        CancellationToken cancellationToken)
    {
        var usuarioId = IdUsuarioActual();

        var viaje = await _context.Viajes.Include(v => v.Conductor).Include(v => v.Vehiculo)
            .FirstOrDefaultAsync(v => v.Id == dto.ViajeId, cancellationToken);
        if (viaje is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Viaje no encontrado", detail: "El viaje no existe.");
        }
        if (await _context.Planillas.AnyAsync(p => p.ViajeId == dto.ViajeId, cancellationToken))
        {
            return PlanillaDuplicadaDelViaje();
        }

        var advertencias = new List<string>();
        var placaViaje = viaje.Vehiculo!.Placa;
        if (!string.IsNullOrWhiteSpace(dto.Placa) && Placa.Normalizar(dto.Placa) != placaViaje)
        {
            advertencias.Add($"La placa leída por el OCR ({Placa.Normalizar(dto.Placa)}) no coincide con la del viaje ({placaViaje}).");
        }

        // El saldo anterior ya no se encadena por el nombre del conductor: es el saldo actual de su
        // cuenta corriente (ver CuentaConductorService). SaldoAnteriorDeclarado queda solo para
        // auditoría y para advertir si lo escrito en papel difiere del saldo real.
        var saldoAnterior = await _cuenta.ObtenerSaldoAsync(viaje.ConductorId, cancellationToken);

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

        // Mapeo explícito: solo se copian los campos permitidos (anti-overposting). Placa y Conductor
        // salen del viaje, nunca del cuerpo de la petición.
        var entidad = new PlanillaViaje
        {
            ViajeId = viaje.Id,
            Placa = placaViaje,
            Conductor = NormalizarConductor(viaje.Conductor!.NombreCompleto),
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
            TotalGastosDeclarado = Declarado(dto.TotalGastosDeclarado),
            SaldoViajeDeclarado = Declarado(dto.SaldoViajeDeclarado),
            SaldoAnteriorDeclarado = Declarado(dto.SaldoAnteriorDeclarado),
            SaldoNuevoDeclarado = Declarado(dto.SaldoNuevoDeclarado),
            TieneDiferencias = Difiere(dto.TotalGastosDeclarado, calculo.TotalGastos)
                || Difiere(dto.SaldoViajeDeclarado, calculo.SaldoViaje)
                || Difiere(dto.SaldoAnteriorDeclarado, saldoAnterior)
                || Difiere(dto.SaldoNuevoDeclarado, calculo.SaldoTotal),
            EstadoRevision = EstadoRevision.Aprobado,
            JsonOcrCrudo = jsonOcr,
            CreadoPorUsuarioId = usuarioId,
            RevisadoPorUsuarioId = usuarioId,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
            Gastos = dto.Gastos.Select((g, idx) => new GastoDetalle
            {
                Concepto = g.Concepto.Trim(),
                // La categoría elegida por el revisor manda; el validador ya garantizó que es una de las 16.
                ConceptoCanonico = string.IsNullOrWhiteSpace(g.Categoria)
                    ? ConceptoNormalizer.Normalizar(g.Concepto)
                    : g.Categoria.Trim().ToUpperInvariant(),
                Etiqueta = string.IsNullOrWhiteSpace(g.Etiqueta) ? null : g.Etiqueta.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(g.Descripcion) ? null : g.Descripcion.Trim(),
                Valor = CalculadoraLiquidacion.Redondear(g.Valor),
                Orden = idx + 1
            }).ToList()
        };

        await using var transaccion = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await _context.Planillas.AddAsync(entidad, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsPlanillaDuplicadaDelViaje(ex))
        {
            return PlanillaDuplicadaDelViaje();
        }

        // La primera planilla de un viaje lo pasa de Abierto a EnRevision: queda bloqueado (el índice
        // único de viajes abiertos lo protege) hasta que se aprueba o se reabre.
        if (viaje.Estado == EstadoViaje.Abierto)
        {
            viaje.Estado = EstadoViaje.EnRevision;
            await _context.SaveChangesAsync(cancellationToken);
        }
        if (transaccion is not null)
        {
            await transaccion.CommitAsync(cancellationToken);
        }

        _logger.LogInformation("Planilla {Id} guardada por el usuario {UsuarioId}.", entidad.Id, usuarioId);
        if (dto.SaldoAnteriorDeclarado is { } declarado && CalculadoraLiquidacion.Redondear(declarado) != entidad.SaldoAnterior)
        {
            advertencias.Add($"El saldo anterior escrito en la planilla ({DescribirSaldo(declarado)}) difiere del saldo actual " +
                $"del conductor ({DescribirSaldo(entidad.SaldoAnterior)}). Se guardó el saldo real.");
        }
        await _auditoria.RegistrarAsync(AccionesAuditoria.PlanillaGuardada, true, usuarioId, "PlanillaViaje", entidad.Id.ToString(),
            new { viajeId = viaje.Id, placa = entidad.Placa, renglones = entidad.Gastos.Count, entidad.TotalGastos, entidad.SaldoTotal });

        return CreatedAtAction(
            nameof(ObtenerPlanillaPorId),
            new { id = entidad.Id },
            new PlanillaGuardadaDto("Planilla aprobada y guardada.", entidad.Id, ADetalle(entidad),
                advertencias.Count > 0 ? advertencias : null));
    }

    private ObjectResult PlanillaDuplicadaDelViaje() =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "El viaje ya tiene planilla",
            detail: "Este viaje ya tiene una planilla guardada.");

    private static bool EsPlanillaDuplicadaDelViaje(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == Data.Configurations.RestriccionesCatalogos.PlanillaViajeUnica;

    private static decimal? Declarado(decimal? valor) => valor is { } v ? CalculadoraLiquidacion.Redondear(v) : null;

    private static bool Difiere(decimal? declarado, decimal calculado) =>
        declarado is { } d && CalculadoraLiquidacion.Redondear(d) != calculado;

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
        TotalGastosDeclarado: p.TotalGastosDeclarado,
        SaldoViajeDeclarado: p.SaldoViajeDeclarado,
        SaldoAnteriorDeclarado: p.SaldoAnteriorDeclarado,
        SaldoNuevoDeclarado: p.SaldoNuevoDeclarado,
        TieneDiferencias: p.TieneDiferencias,
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
            Orden: g.Orden,
            Etiqueta: g.Etiqueta,
            Descripcion: g.Descripcion
        )).ToList()
    );
}
