using LogisticaBackend.Auth;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticaBackend.Controllers;

/// <summary>Gestión de usuarios. No hay registro público: solo un Administrador crea cuentas.</summary>
[ApiController]
[Route("api/usuarios")]
[Produces("application/json")]
[Authorize(Policy = Politicas.SoloAdministrador)]
public class UsuariosController : ControllerBase
{
    private readonly UserManager<Usuario> _userManager;
    private readonly TimeProvider _reloj;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(UserManager<Usuario> userManager, TimeProvider reloj, ILogger<UsuariosController> logger)
    {
        _userManager = userManager;
        _reloj = reloj;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var usuarios = await _userManager.Users.AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(cancellationToken);

        var resultado = new List<UsuarioDto>(usuarios.Count);
        foreach (var u in usuarios)
        {
            resultado.Add(await ADtoAsync(u));
        }
        return Ok(resultado);
    }

    /// <summary>Crea el usuario con una contraseña temporal que se muestra una sola vez.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioCreadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioDto dto)
    {
        var email = dto.Email.Trim();
        var usuario = new Usuario
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            NombreCompleto = dto.NombreCompleto.Trim(),
            Activo = true,
            DebeCambiarPassword = true,
            CreadoEn = _reloj.GetUtcNow().UtcDateTime,
        };

        var passwordTemporal = GeneradorPassword.Generar();
        var creado = await _userManager.CreateAsync(usuario, passwordTemporal);
        if (!creado.Succeeded)
        {
            return ErroresIdentity(creado, nameof(CrearUsuarioDto.Email));
        }
        await _userManager.AddToRoleAsync(usuario, dto.Rol);

        _logger.LogInformation("Administrador {AdminId} creó el usuario {UsuarioId} con rol {Rol}.", IdActual(), usuario.Id, dto.Rol);
        return StatusCode(StatusCodes.Status201Created, new UsuarioCreadoDto(await ADtoAsync(usuario), passwordTemporal));
    }

    [HttpPut("{id:guid}/rol")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarRol(Guid id, [FromBody] CambiarRolDto dto)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NoEncontrado();

        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        if (rolesActuales.Contains(Roles.Administrador) && dto.Rol != Roles.Administrador
            && await EsUltimoAdministradorActivoAsync(usuario))
        {
            return Conflicto("No se puede quitar el rol al último administrador activo.");
        }

        await _userManager.RemoveFromRolesAsync(usuario, rolesActuales);
        await _userManager.AddToRoleAsync(usuario, dto.Rol);
        await _userManager.UpdateSecurityStampAsync(usuario); // la sesión abierta toma el rol nuevo

        _logger.LogInformation("Administrador {AdminId} cambió el rol del usuario {UsuarioId} a {Rol}.", IdActual(), usuario.Id, dto.Rol);
        return Ok(await ADtoAsync(usuario));
    }

    [HttpPut("{id:guid}/estado")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoDto dto)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NoEncontrado();

        if (!dto.Activo)
        {
            if (usuario.Id == IdActual())
            {
                return Conflicto("No puede desactivar su propia cuenta.");
            }
            if (await _userManager.IsInRoleAsync(usuario, Roles.Administrador) && await EsUltimoAdministradorActivoAsync(usuario))
            {
                return Conflicto("No se puede desactivar al último administrador activo.");
            }

            usuario.Activo = false;
            await _userManager.UpdateAsync(usuario);
            await _userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
        }
        else
        {
            usuario.Activo = true;
            await _userManager.UpdateAsync(usuario);
            await _userManager.SetLockoutEndDateAsync(usuario, null);
            await _userManager.ResetAccessFailedCountAsync(usuario);
        }

        // Rotar el stamp cierra las sesiones abiertas en la siguiente validación.
        await _userManager.UpdateSecurityStampAsync(usuario);

        _logger.LogInformation("Administrador {AdminId} cambió el estado del usuario {UsuarioId}. Activo: {Activo}.", IdActual(), usuario.Id, dto.Activo);
        return Ok(await ADtoAsync(usuario));
    }

    /// <summary>Desactiva el 2FA del usuario y cierra sus sesiones. Deberá configurarlo de nuevo al entrar.</summary>
    [HttpPost("{id:guid}/reiniciar-2fa")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reiniciar2fa(Guid id)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null) return NoEncontrado();

        await _userManager.SetTwoFactorEnabledAsync(usuario, false);
        await _userManager.ResetAuthenticatorKeyAsync(usuario);
        await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 0); // invalida los códigos anteriores
        await _userManager.UpdateSecurityStampAsync(usuario);

        _logger.LogInformation("Administrador {AdminId} reinició el 2FA del usuario {UsuarioId}.", IdActual(), usuario.Id);
        return Ok(await ADtoAsync(usuario));
    }

    // ---------------------------------------------------------------- auxiliares

    private async Task<bool> EsUltimoAdministradorActivoAsync(Usuario usuario)
    {
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrador);
        return !admins.Any(a => a.Id != usuario.Id && a.Activo);
    }

    private async Task<UsuarioDto> ADtoAsync(Usuario u)
    {
        var rol = (await _userManager.GetRolesAsync(u)).FirstOrDefault();
        var bloqueado = u.LockoutEnd.HasValue && u.LockoutEnd.Value > _reloj.GetUtcNow();
        return new UsuarioDto(u.Id, u.Email!, u.NombreCompleto, rol, u.Activo, u.TwoFactorEnabled,
            u.DebeCambiarPassword, bloqueado, u.CreadoEn, u.UltimoAcceso);
    }

    private Guid? IdActual() => Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : null;

    private ObjectResult NoEncontrado() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: "El usuario no existe.");

    private ObjectResult Conflicto(string detalle) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Operación no permitida", detail: detalle);

    private static ObjectResult ErroresIdentity(IdentityResult resultado, string campo) =>
        new(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [campo] = resultado.Errors.Select(e => e.Description).ToArray()
        })
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Error de validación",
            Detail = "Uno o más campos no cumplen las reglas.",
        })
        { StatusCode = StatusCodes.Status422UnprocessableEntity };
}
