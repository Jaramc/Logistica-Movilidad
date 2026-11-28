using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Data.Configurations;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;

namespace LogisticaBackend.Controllers;

[ApiController]
[Route("api/viajes")]
[Produces("application/json")]
public class ViajesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICuentaConductorService _cuenta;
    private readonly IAuditoriaService _auditoria;
    private readonly TimeProvider _reloj;

    public ViajesController(AppDbContext db, ICuentaConductorService cuenta, IAuditoriaService auditoria, TimeProvider reloj)
    {
        _db = db;
        _cuenta = cuenta;
        _auditoria = auditoria;
        _reloj = reloj;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(PagedResultDto<ViajeResumenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] ConsultaViajesDto consulta, CancellationToken cancellationToken)
    {
        var query = _db.Viajes.AsNoTracking().Include(v => v.Conductor).Include(v => v.Vehiculo).AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Estado) && Enum.TryParse<EstadoViaje>(consulta.Estado, out var estado))
        {
            query = query.Where(v => v.Estado == estado);
        }
        if (consulta.ConductorId is { } conductorId) query = query.Where(v => v.ConductorId == conductorId);
        if (!string.IsNullOrWhiteSpace(consulta.Placa))
        {
            var placa = Domain.Services.Placa.Normalizar(consulta.Placa);
            query = query.Where(v => v.Vehiculo!.Placa.StartsWith(placa));
        }
        if (consulta.Desde is { } desde) query = query.Where(v => v.FechaInicio >= desde);
        if (consulta.Hasta is { } hasta) query = query.Where(v => v.FechaInicio <= hasta);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)consulta.Tamano);

        var items = await query
            .OrderByDescending(v => v.FechaInicio).ThenByDescending(v => v.Id)
            .Skip((consulta.Pagina - 1) * consulta.Tamano)
            .Take(consulta.Tamano)
            .Select(v => new ViajeResumenDto(
                v.Id, v.ConductorId, v.Conductor!.Codigo, v.Conductor.NombreCompleto, v.VehiculoId, v.Vehiculo!.Placa,
                v.Origen, v.Destino, v.Cliente, v.Flete, v.FechaInicio, v.FechaFin,
                v.Estado.ToString(), v.SaldoViaje, v.AprobadoEn))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<ViajeResumenDto>(items, totalCount, consulta.Pagina, consulta.Tamano, totalPages));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(ViajeDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken)
    {
        var viaje = await CargarViajeAsync(id, cancellationToken);
        if (viaje is null) return NoEncontradoViaje();
        return Ok(await ADetalleAsync(viaje, cancellationToken));
    }

    /// <summary>Abre un viaje. Solo Revisor o Administrador.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.PuedeRevisar)]
    [ProducesResponseType(typeof(ViajeDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Abrir([FromBody] AbrirViajeDto dto, CancellationToken cancellationToken)
    {
        var conductor = await _db.Conductores.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.ConductorId, cancellationToken);
        if (conductor is null || !conductor.Activo)
        {
            return Problemas.Validacion(HttpContext, new Dictionary<string, string[]> { ["conductorId"] = new[] { "El conductor no existe o está inactivo." } });
        }
        var vehiculo = await _db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == dto.VehiculoId, cancellationToken);
        if (vehiculo is null || !vehiculo.Activo)
        {
            return Problemas.Validacion(HttpContext, new Dictionary<string, string[]> { ["vehiculoId"] = new[] { "El vehículo no existe o está inactivo." } });
        }

        var viaje = new Viaje
        {
            ConductorId = dto.ConductorId,
            VehiculoId = dto.VehiculoId,
            Origen = dto.Origen.Trim(),
            Destino = dto.Destino.Trim(),
            Cliente = dto.Cliente.Trim(),
            Manifiesto = string.IsNullOrWhiteSpace(dto.Manifiesto) ? null : dto.Manifiesto.Trim(),
            Flete = dto.Flete,
            FechaInicio = dto.FechaInicio,
            Estado = EstadoViaje.Abierto,
            CreadoPorUsuarioId = IdActual(),
        };
        _db.Viajes.Add(viaje);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViajeAbiertoDuplicado(ex, out var campo))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Ya hay un viaje abierto",
                detail: campo == "conductor"
                    ? "Este conductor ya tiene un viaje abierto o en revisión."
                    : "Este vehículo ya tiene un viaje abierto o en revisión.");
        }

        await _auditoria.RegistrarAsync(AccionesAuditoria.ViajeAbierto, true, IdActual(), "Viaje", viaje.Id.ToString(),
            new { viaje.ConductorId, viaje.VehiculoId }, cancellationToken);

        var detalle = await ADetalleAsync((await CargarViajeAsync(viaje.Id, cancellationToken))!, cancellationToken);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = viaje.Id }, detalle);
    }

    [HttpPost("{id:int}/traslados")]
    [Authorize(Policy = Politicas.PuedeRevisar)]
    [ProducesResponseType(typeof(TrasladoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarTraslado(int id, [FromBody] RegistrarTrasladoDto dto, CancellationToken cancellationToken)
    {
        var viaje = await _db.Viajes.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (viaje is null) return NoEncontradoViaje();
        if (viaje.Estado is not (EstadoViaje.Abierto or EstadoViaje.EnRevision))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Viaje ya aprobado",
                detail: "El viaje ya fue aprobado; no se pueden registrar más traslados.");
        }

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var traslado = new Traslado
        {
            ViajeId = id,
            Fecha = dto.Fecha,
            Valor = CalculadoraLiquidacionRedondear(dto.Valor),
            Medio = Enum.Parse<MedioTraslado>(dto.Medio),
            Nota = string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim(),
            RegistradoPorUsuarioId = IdActual(),
            CreadoEn = ahora,
        };
        _db.Traslados.Add(traslado);
        // Reescribe la fila del viaje en el mismo SaveChanges: si alguien lo aprobó entre la lectura y
        // este guardado, el xmin no coincide y EF lanza DbUpdateConcurrencyException (409), sin traslado.
        viaje.ActualizadoEn = ahora;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(AccionesAuditoria.TrasladoRegistrado, true, IdActual(), "Traslado", traslado.Id.ToString(),
            new { viajeId = id, traslado.Valor }, cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            new TrasladoDto(traslado.Id, traslado.Fecha, traslado.Valor, traslado.Medio.ToString(), traslado.Nota, traslado.CreadoEn));
    }

    /// <summary>
    /// Exige planilla + al menos un traslado (422 si falta). Si el anticipo escrito en la planilla
    /// difiere de la suma de traslados, guarda igual pero devuelve una advertencia.
    /// </summary>
    [HttpPost("{id:int}/aprobar")]
    [Authorize(Policy = Politicas.PuedeRevisar)]
    [ProducesResponseType(typeof(AprobarViajeRespuestaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aprobar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _cuenta.CerrarViajeAsync(id, IdActual(), cancellationToken);

        var advertencias = new List<string>();
        if (resultado.AnticipoDeclarado != resultado.AnticipoTraslados)
        {
            advertencias.Add("El anticipo escrito en la planilla no coincide con los traslados registrados.");
        }

        var viaje = await CargarViajeAsync(id, cancellationToken);
        var detalle = await ADetalleAsync(viaje!, cancellationToken);
        return Ok(new AprobarViajeRespuestaDto(detalle, advertencias.Count > 0 ? advertencias : null));
    }

    /// <summary>Solo Administrador, y solo si el viaje no tiene pagos. Revierte el CierreViaje y lo deja Abierto.</summary>
    [HttpPost("{id:int}/reabrir")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(ViajeDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reabrir(int id, [FromBody] ReabrirViajeDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Motivo requerido",
                detail: "Debe indicar el motivo de la reapertura.");
        }

        await _cuenta.ReabrirViajeAsync(id, dto.Motivo.Trim(), IdActual(), cancellationToken);

        var viaje = await CargarViajeAsync(id, cancellationToken);
        return Ok(await ADetalleAsync(viaje!, cancellationToken));
    }

    // ---------------------------------------------------------------- auxiliares

    private Task<Viaje?> CargarViajeAsync(int id, CancellationToken cancellationToken) =>
        _db.Viajes.AsNoTracking()
            .Include(v => v.Conductor).Include(v => v.Vehiculo).Include(v => v.Traslados).Include(v => v.Planilla)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    private async Task<ViajeDetalleDto> ADetalleAsync(Viaje v, CancellationToken cancellationToken)
    {
        var anticipoTraslados = v.Traslados.Sum(t => t.Valor);
        var pendiente = await _cuenta.ObtenerPendienteViajeAsync(v.Id, cancellationToken);
        return new ViajeDetalleDto(
            v.Id, v.ConductorId, v.Conductor!.Codigo, v.Conductor.NombreCompleto, v.VehiculoId, v.Vehiculo!.Placa,
            v.Origen, v.Destino, v.Cliente, v.Manifiesto, v.Flete, v.FechaInicio, v.FechaFin, v.Estado.ToString(),
            v.SaldoViaje, pendiente, anticipoTraslados, v.Planilla?.Id,
            v.Traslados.OrderBy(t => t.Fecha).Select(t => new TrasladoDto(t.Id, t.Fecha, t.Valor, t.Medio.ToString(), t.Nota, t.CreadoEn)).ToList());
    }

    private Guid? IdActual()
    {
        var valor = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private static decimal CalculadoraLiquidacionRedondear(decimal valor) => Domain.Services.CalculadoraLiquidacion.Redondear(valor);

    private static bool EsViajeAbiertoDuplicado(DbUpdateException ex, out string campo)
    {
        campo = string.Empty;
        if (ex.InnerException is not Npgsql.PostgresException pg || pg.SqlState != Npgsql.PostgresErrorCodes.UniqueViolation) return false;
        if (pg.ConstraintName == RestriccionesCatalogos.ConductorAbiertoUnico) { campo = "conductor"; return true; }
        if (pg.ConstraintName == RestriccionesCatalogos.VehiculoAbiertoUnico) { campo = "vehiculo"; return true; }
        return false;
    }

    private ObjectResult NoEncontradoViaje() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: "El viaje no existe.");
}
