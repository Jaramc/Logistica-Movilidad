using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;

namespace LogisticaBackend.Controllers;

/// <summary>
/// Catálogo de conductores y su cuenta corriente. Lectura para cualquier rol autenticado; toda
/// escritura exige Administrador. La cédula se devuelve enmascarada salvo para ese rol.
/// </summary>
[ApiController]
[Route("api/conductores")]
[Produces("application/json")]
public class ConductoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICuentaConductorService _cuenta;
    private readonly IAuditoriaService _auditoria;

    public ConductoresController(AppDbContext db, ICuentaConductorService cuenta, IAuditoriaService auditoria)
    {
        _db = db;
        _cuenta = cuenta;
        _auditoria = auditoria;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(IReadOnlyList<ConductorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] bool? activo, CancellationToken cancellationToken)
    {
        var query = _db.Conductores.AsNoTracking();
        if (activo is { } a) query = query.Where(c => c.Activo == a);

        var conductores = await query.OrderBy(c => c.Codigo).ToListAsync(cancellationToken);
        var resultado = new List<ConductorDto>(conductores.Count);
        foreach (var c in conductores)
        {
            resultado.Add(await ADtoAsync(c, cancellationToken));
        }
        return Ok(resultado);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(ConductorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken)
    {
        var conductor = await _db.Conductores.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conductor is null) return NoEncontrado();
        return Ok(await ADtoAsync(conductor, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(ConductorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearConductorDto dto, CancellationToken cancellationToken)
    {
        var siguiente = await _db.Database.SqlQuery<int>($"SELECT nextval('seq_conductores_codigo') AS \"Value\"").SingleAsync(cancellationToken);
        var conductor = new Conductor
        {
            Codigo = "C" + siguiente.ToString("D2"),
            NombreCompleto = dto.NombreCompleto.Trim(),
            Cedula = dto.Cedula.Trim(),
        };
        _db.Conductores.Add(conductor);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsCedulaDuplicada(ex))
        {
            return ErrorCedulaDuplicada();
        }

        await _auditoria.RegistrarAsync(AccionesAuditoria.ConductorCreado, true, IdActual(), "Conductor", conductor.Id.ToString(),
            new { conductor.Codigo }, cancellationToken);

        return CreatedAtAction(nameof(ObtenerPorId), new { id = conductor.Id }, await ADtoAsync(conductor, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(ConductorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarConductorDto dto, CancellationToken cancellationToken)
    {
        var conductor = await _db.Conductores.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conductor is null) return NoEncontrado();

        conductor.NombreCompleto = dto.NombreCompleto.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(AccionesAuditoria.ConductorActualizado, true, IdActual(), "Conductor", conductor.Id.ToString(), null, cancellationToken);

        return Ok(await ADtoAsync(conductor, cancellationToken));
    }

    /// <summary>Nunca se borra un conductor: solo se desactiva.</summary>
    [HttpPost("{id:int}/desactivar")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(ConductorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        var conductor = await _db.Conductores.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conductor is null) return NoEncontrado();

        conductor.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(AccionesAuditoria.ConductorDesactivado, true, IdActual(), "Conductor", conductor.Id.ToString(), null, cancellationToken);

        return Ok(await ADtoAsync(conductor, cancellationToken));
    }

    [HttpPost("{id:int}/saldo-inicial")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(MovimientoSaldoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarSaldoInicial(int id, [FromBody] SaldoInicialDto dto, CancellationToken cancellationToken)
    {
        var movimiento = await _cuenta.RegistrarSaldoInicialAsync(id, dto.Valor, dto.FechaCorte, dto.Justificacion.Trim(), IdActual(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ADto(movimiento));
    }

    [HttpGet("{id:int}/movimientos")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(PagedResultDto<MovimientoSaldoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerMovimientos(int id, [FromQuery] int pagina = 1, [FromQuery] int tamano = 20, CancellationToken cancellationToken = default)
    {
        pagina = Math.Max(1, pagina);
        tamano = Math.Clamp(tamano, 1, 100);

        var (items, total) = await _cuenta.ObtenerMovimientosAsync(id, pagina, tamano, cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)tamano);
        return Ok(new PagedResultDto<MovimientoSaldoDto>(items.Select(ADto).ToList(), total, pagina, tamano, totalPages));
    }

    [HttpPost("{id:int}/pagos")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(MovimientoSaldoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarPago(int id, [FromBody] RegistrarPagoDto dto, CancellationToken cancellationToken)
    {
        var tipo = Enum.Parse<TipoPago>(dto.Tipo);
        var movimiento = await _cuenta.RegistrarPagoAsync(id, dto.ViajeId, tipo, dto.Valor, dto.Fecha,
            dto.Quincena, dto.Medio, dto.Nota, IdActual(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ADto(movimiento));
    }

    [HttpPost("{id:int}/ajustes")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(MovimientoSaldoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarAjuste(int id, [FromBody] RegistrarAjusteDto dto, CancellationToken cancellationToken)
    {
        var movimiento = await _cuenta.RegistrarAjusteAsync(id, dto.Valor, dto.Justificacion.Trim(), IdActual(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ADto(movimiento));
    }

    // ---------------------------------------------------------------- auxiliares

    private async Task<ConductorDto> ADtoAsync(Conductor c, CancellationToken cancellationToken)
    {
        var tieneMovimientos = await _db.MovimientosSaldo.AsNoTracking().AnyAsync(m => m.ConductorId == c.Id, cancellationToken);
        var saldo = tieneMovimientos ? await _cuenta.ObtenerSaldoAsync(c.Id, cancellationToken) : 0m;
        return new ConductorDto(c.Id, c.Codigo, c.NombreCompleto, Enmascarar(c.Cedula), c.Activo, c.CreadoEn, saldo, tieneMovimientos);
    }

    private static MovimientoSaldoDto ADto(MovimientoSaldo m) =>
        new(m.Id, m.Tipo.ToString(), m.Valor, m.SaldoResultante, m.FechaCorte, m.Justificacion, m.ViajeId, m.PagoSaldoId, m.CreadoEn);

    /// <summary>"••••3223": solo los últimos 4 dígitos, salvo para el rol Administrador.</summary>
    private string Enmascarar(string cedula)
    {
        if (User.IsInRole(Roles.Administrador) || cedula.Length <= 4) return cedula;
        return new string('•', cedula.Length - 4) + cedula[^4..];
    }

    private Guid? IdActual()
    {
        var valor = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private static bool EsCedulaDuplicada(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == Data.Configurations.RestriccionesCatalogos.CedulaUnica;

    private ObjectResult ErrorCedulaDuplicada() =>
        Problemas.Validacion(HttpContext, new Dictionary<string, string[]> { ["cedula"] = new[] { "Ya existe un conductor con esa cédula." } },
            StatusCodes.Status409Conflict, "Cédula duplicada", "Ya existe un conductor con esa cédula.");

    private ObjectResult NoEncontrado() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: "El conductor no existe.");
}
