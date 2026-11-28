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
[Route("api/vehiculos")]
[Produces("application/json")]
public class VehiculosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public VehiculosController(AppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.PuedeLeer)]
    [ProducesResponseType(typeof(IReadOnlyList<VehiculoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] bool? activo, CancellationToken cancellationToken)
    {
        var query = _db.Vehiculos.AsNoTracking();
        if (activo is { } a) query = query.Where(v => v.Activo == a);

        var vehiculos = await query.OrderBy(v => v.Placa)
            .Select(v => new VehiculoDto(v.Id, v.Placa, v.Tipo, v.CategoriaPeaje, v.Activo))
            .ToListAsync(cancellationToken);
        return Ok(vehiculos);
    }

    [HttpPost]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearVehiculoDto dto, CancellationToken cancellationToken)
    {
        var vehiculo = new Vehiculo
        {
            Placa = Domain.Services.Placa.Normalizar(dto.Placa),
            Tipo = string.IsNullOrWhiteSpace(dto.Tipo) ? "Tractomula" : dto.Tipo.Trim(),
        };
        _db.Vehiculos.Add(vehiculo);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsPlacaDuplicada(ex))
        {
            return Problemas.Validacion(HttpContext, new Dictionary<string, string[]> { ["placa"] = new[] { "Ya existe un vehículo con esa placa." } },
                StatusCodes.Status409Conflict, "Placa duplicada", "Ya existe un vehículo con esa placa.");
        }

        await _auditoria.RegistrarAsync(AccionesAuditoria.VehiculoCreado, true, IdActual(), "Vehiculo", vehiculo.Id.ToString(),
            new { vehiculo.Placa }, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new VehiculoDto(vehiculo.Id, vehiculo.Placa, vehiculo.Tipo, vehiculo.CategoriaPeaje, vehiculo.Activo));
    }

    /// <summary>Nunca se borra un vehículo: solo se desactiva.</summary>
    [HttpPost("{id:int}/desactivar")]
    [Authorize(Policy = Politicas.SoloAdministrador)]
    [ProducesResponseType(typeof(VehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        var vehiculo = await _db.Vehiculos.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehiculo is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: "El vehículo no existe.");
        }

        vehiculo.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        await _auditoria.RegistrarAsync(AccionesAuditoria.VehiculoDesactivado, true, IdActual(), "Vehiculo", vehiculo.Id.ToString(), null, cancellationToken);

        return Ok(new VehiculoDto(vehiculo.Id, vehiculo.Placa, vehiculo.Tipo, vehiculo.CategoriaPeaje, vehiculo.Activo));
    }

    private Guid? IdActual()
    {
        var valor = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }

    private static bool EsPlacaDuplicada(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == RestriccionesCatalogos.PlacaUnica;
}
