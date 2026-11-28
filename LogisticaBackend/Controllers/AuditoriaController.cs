using LogisticaBackend.Auth;
using LogisticaBackend.Data;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticaBackend.Controllers;

/// <summary>Consulta de la auditoría. Solo lectura: no existe ningún endpoint para editar ni borrar.</summary>
[ApiController]
[Route("api/auditoria")]
[Produces("application/json")]
[Authorize(Policy = Politicas.SoloAdministrador)]
public class AuditoriaController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuditoriaController(AppDbContext context) => _context = context;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<RegistroAuditoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] ConsultaAuditoriaDto consulta, CancellationToken cancellationToken)
    {
        var query = _context.Auditoria.AsNoTracking();

        if (consulta.Desde is { } desde)
        {
            var inicio = InicioDelDiaUtc(desde);
            query = query.Where(a => a.FechaUtc >= inicio);
        }
        if (consulta.Hasta is { } hasta)
        {
            var fin = InicioDelDiaUtc(hasta.AddDays(1));
            query = query.Where(a => a.FechaUtc < fin);
        }
        if (!string.IsNullOrWhiteSpace(consulta.Accion)) query = query.Where(a => a.Accion == consulta.Accion);
        if (consulta.UsuarioId is { } usuarioId) query = query.Where(a => a.UsuarioId == usuarioId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.FechaUtc).ThenByDescending(a => a.Id)
            .Skip((consulta.Pagina - 1) * consulta.Tamano)
            .Take(consulta.Tamano)
            .GroupJoin(_context.Users, a => a.UsuarioId, u => u.Id, (a, us) => new { a, us })
            .SelectMany(x => x.us.DefaultIfEmpty(), (x, u) => new RegistroAuditoriaDto(
                x.a.Id, x.a.FechaUtc, x.a.UsuarioId, u != null ? u.Email : null, x.a.Accion,
                x.a.Entidad, x.a.EntidadId, x.a.Ip, x.a.Detalle, x.a.Exito))
            .ToListAsync(cancellationToken);

        var paginas = (int)Math.Ceiling(total / (double)consulta.Tamano);
        return Ok(new PagedResultDto<RegistroAuditoriaDto>(items, total, consulta.Pagina, consulta.Tamano, paginas));
    }

    /// <summary>Los filtros de fecha se interpretan en hora de Colombia.</summary>
    private static DateTime InicioDelDiaUtc(DateOnly fecha) =>
        new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue), FechaNegocio.DesfaseColombia).UtcDateTime;
}
