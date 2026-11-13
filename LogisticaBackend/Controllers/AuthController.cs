using System.Security.Claims;
using System.Text;
using LogisticaBackend.Auth;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;
using LogisticaBackend.Middleware;
using LogisticaBackend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    public const string MensajeLoginFallido = "Usuario o contraseña incorrectos.";
    public const string MensajeCodigoFallido = "El código no es válido o la verificación expiró. Inicie sesión de nuevo.";
    private const int CantidadCodigosRecuperacion = 10;

    private readonly UserManager<Usuario> _userManager;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly SeguridadOptions _seguridad;
    private readonly TimeProvider _reloj;
    private readonly LimitadorLoginPorUsuario _limitador;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        IOptions<SeguridadOptions> seguridad,
        TimeProvider reloj,
        LimitadorLoginPorUsuario limitador,
        IAuditoriaService auditoria,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _seguridad = seguridad.Value;
        _reloj = reloj;
        _limitador = limitador;
        _auditoria = auditoria;
        _logger = logger;
    }

    /// <summary>
    /// Entrega la cookie XSRF-TOKEN (legible por JavaScript). El frontend la llama al cargar;
    /// las respuestas de login, 2FA y logout la renuevan solas porque el token va ligado al usuario.
    /// </summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Antiforgery()
    {
        ConfiguracionAntiforgery.EmitirTokens(HttpContext);
        return NoContent();
    }

    /// <summary>Primer paso: correo y contraseña. Respuesta idéntica si el usuario no existe, está bloqueado o inactivo.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(LimitesPeticiones.PoliticaLogin)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginRespuestaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (_limitador.Intentar("correo:" + dto.Email) is { } espera)
        {
            return LimiteExcedido(espera);
        }

        var usuario = await _userManager.FindByEmailAsync(dto.Email.Trim());
        if (usuario is null)
        {
            HashFicticio.Verificar(_userManager.PasswordHasher, dto.Password);
            _logger.LogInformation("Login fallido: usuario inexistente.");
            // No se guarda el correo intentado: puede ser una contraseña escrita en el campo equivocado.
            await _auditoria.RegistrarAsync(AccionesAuditoria.LoginFallido, false, detalle: new { motivo = "usuario_inexistente" });
            return LoginFallido();
        }

        var bloqueadoAntes = await _userManager.IsLockedOutAsync(usuario);
        var resultado = await _signInManager.PasswordSignInAsync(usuario, dto.Password, isPersistent: false, lockoutOnFailure: true);

        if (resultado.RequiresTwoFactor)
        {
            await _auditoria.RegistrarAsync(AccionesAuditoria.LoginExitoso, true, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { paso = "password", requiere2fa = true });
            return Ok(new LoginRespuestaDto(Requiere2fa: true));
        }

        if (resultado.Succeeded)
        {
            // Solo ocurre si el usuario aún no tiene 2FA: la sesión queda limitada a configurarlo.
            await RegistrarAccesoAsync(usuario);
            await RenovarTokenCsrfAsync(usuario);
            await _auditoria.RegistrarAsync(AccionesAuditoria.LoginExitoso, true, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { paso = "password", requiere2fa = false, sesionLimitada = true });
            return Ok(new LoginRespuestaDto(Requiere2fa: false));
        }

        if (resultado.IsLockedOut || resultado.IsNotAllowed)
        {
            // Identity no verifica la contraseña en estos casos; se iguala el tiempo de respuesta.
            HashFicticio.Verificar(_userManager.PasswordHasher, dto.Password);
        }
        _logger.LogInformation("Login fallido para el usuario {UsuarioId}. Bloqueado: {Bloqueado}. Inactivo: {Inactivo}.",
            usuario.Id, resultado.IsLockedOut, resultado.IsNotAllowed);
        var motivo = resultado.IsNotAllowed ? "inactivo" : resultado.IsLockedOut ? "bloqueado" : "credenciales";
        await _auditoria.RegistrarAsync(AccionesAuditoria.LoginFallido, false, usuario.Id, "Usuario", usuario.Id.ToString(), new { motivo });
        await AuditarBloqueoSiOcurrioAsync(usuario, bloqueadoAntes, "password");
        return LoginFallido();
    }

    /// <summary>Segundo paso con el código de la app autenticadora.</summary>
    [HttpPost("login-2fa")]
    [EnableRateLimiting(LimitesPeticiones.PoliticaLogin)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsuarioActualDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login2fa([FromBody] CodigoDto dto)
    {
        var usuario = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
        {
            return CodigoFallido();
        }
        if (_limitador.Intentar("usuario:" + usuario.Id) is { } espera)
        {
            return LimiteExcedido(espera);
        }

        var bloqueadoAntes = await _userManager.IsLockedOutAsync(usuario);
        var codigo = NormalizarCodigoTotp(dto.Codigo);
        var resultado = await _signInManager.TwoFactorAuthenticatorSignInAsync(codigo, isPersistent: false, rememberClient: false);
        return await ResultadoSegundoFactorAsync(usuario, resultado, "totp", bloqueadoAntes);
    }

    /// <summary>Segundo paso con un código de recuperación (cada código sirve una sola vez).</summary>
    [HttpPost("login-recuperacion")]
    [EnableRateLimiting(LimitesPeticiones.PoliticaLogin)]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsuarioActualDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginRecuperacion([FromBody] CodigoDto dto)
    {
        var usuario = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (usuario is null)
        {
            return CodigoFallido();
        }
        if (_limitador.Intentar("usuario:" + usuario.Id) is { } espera)
        {
            return LimiteExcedido(espera);
        }

        var bloqueadoAntes = await _userManager.IsLockedOutAsync(usuario);
        var resultado = await _signInManager.TwoFactorRecoveryCodeSignInAsync(UsuarioStore.NormalizarCodigo(dto.Codigo));
        return await ResultadoSegundoFactorAsync(usuario, resultado, "recuperacion", bloqueadoAntes);
    }

    [HttpPost("logout")]
    [Authorize(Policy = Politicas.SesionIniciada)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var id = IdActual();
        await _signInManager.SignOutAsync();
        await RenovarTokenCsrfAsync(null);
        await _auditoria.RegistrarAsync(AccionesAuditoria.Logout, true, id, "Usuario", id?.ToString());
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = Politicas.SesionIniciada)]
    [ProducesResponseType(typeof(UsuarioActualDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null)
        {
            await _signInManager.SignOutAsync();
            return Unauthorized();
        }
        return Ok(await CrearUsuarioActualAsync(usuario, User.HasClaim(ClaimsSeguridad.Amr, ClaimsSeguridad.AmrMfa)));
    }

    [HttpPost("cambiar-password")]
    [Authorize(Policy = Politicas.SesionIniciada)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) return Unauthorized();

        var resultado = await _userManager.ChangePasswordAsync(usuario, dto.PasswordActual, dto.PasswordNueva);
        if (!resultado.Succeeded)
        {
            if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                await _userManager.AccessFailedAsync(usuario); // evita adivinar la contraseña actual sin límite
            }
            await _auditoria.RegistrarAsync(AccionesAuditoria.CambioPassword, false, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { errores = resultado.Errors.Select(e => e.Code).ToArray() });
            return ErroresIdentity(resultado, nameof(CambiarPasswordDto.PasswordNueva));
        }

        usuario.DebeCambiarPassword = false;
        await _userManager.UpdateAsync(usuario);
        await _signInManager.RefreshSignInAsync(usuario); // conserva amr=mfa y el inicio de la sesión
        _logger.LogInformation("Usuario {UsuarioId} cambió su contraseña.", usuario.Id);
        await _auditoria.RegistrarAsync(AccionesAuditoria.CambioPassword, true, usuario.Id, "Usuario", usuario.Id.ToString());
        return NoContent();
    }

    /// <summary>Genera una clave TOTP nueva. Solo si el usuario aún no tiene 2FA activo.</summary>
    [HttpPost("2fa/configurar")]
    [Authorize(Policy = Politicas.SesionIniciada)]
    [ProducesResponseType(typeof(Configurar2faRespuestaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Configurar2fa()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) return Unauthorized();
        if (usuario.TwoFactorEnabled) return Ya2faConfigurado();

        await _userManager.ResetAuthenticatorKeyAsync(usuario); // rota el security stamp
        await _signInManager.RefreshSignInAsync(usuario);
        var clave = await _userManager.GetAuthenticatorKeyAsync(usuario) ?? throw new InvalidOperationException("No se generó la clave TOTP.");

        return Ok(new Configurar2faRespuestaDto(FormatearClave(clave), CrearUriOtpauth(usuario.Email!, clave)));
    }

    /// <summary>Confirma el primer código TOTP, activa el 2FA y entrega los códigos de recuperación una sola vez.</summary>
    [HttpPost("2fa/verificar")]
    [Authorize(Policy = Politicas.SesionIniciada)]
    [ProducesResponseType(typeof(CodigosRecuperacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Verificar2fa([FromBody] CodigoDto dto)
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) return Unauthorized();
        if (usuario.TwoFactorEnabled) return Ya2faConfigurado();

        if (await _userManager.IsLockedOutAsync(usuario))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Código incorrecto",
                detail: "Demasiados intentos fallidos. Espere unos minutos e intente de nuevo.");
        }

        var valido = await _userManager.VerifyTwoFactorTokenAsync(
            usuario, _userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizarCodigoTotp(dto.Codigo));
        if (!valido)
        {
            await _userManager.AccessFailedAsync(usuario);
            await _auditoria.RegistrarAsync(AccionesAuditoria.DosFactoresConfigurado, false, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { motivo = "codigo_incorrecto" });
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Código incorrecto",
                detail: "El código no coincide. Revise la hora de su teléfono e intente de nuevo.");
        }

        await _userManager.SetTwoFactorEnabledAsync(usuario, true);
        await _userManager.ResetAccessFailedCountAsync(usuario);
        var codigos = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, CantidadCodigosRecuperacion))?.ToList() ?? new();

        // Nueva sesión, ya con segundo factor.
        await _signInManager.SignInWithClaimsAsync(usuario, isPersistent: false,
            new[] { new Claim(ClaimsSeguridad.Amr, ClaimsSeguridad.AmrMfa) });
        await RenovarTokenCsrfAsync(usuario);
        _logger.LogInformation("Usuario {UsuarioId} activó 2FA.", usuario.Id);
        await _auditoria.RegistrarAsync(AccionesAuditoria.DosFactoresConfigurado, true, usuario.Id, "Usuario", usuario.Id.ToString());

        return Ok(new CodigosRecuperacionDto(codigos));
    }

    /// <summary>Invalida los códigos de recuperación anteriores y entrega 10 nuevos.</summary>
    [HttpPost("2fa/regenerar-codigos")]
    [ProducesResponseType(typeof(CodigosRecuperacionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerarCodigos()
    {
        var usuario = await _userManager.GetUserAsync(User);
        if (usuario is null) return Unauthorized();

        var codigos = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, CantidadCodigosRecuperacion))?.ToList() ?? new();
        await _signInManager.RefreshSignInAsync(usuario);
        _logger.LogInformation("Usuario {UsuarioId} regeneró sus códigos de recuperación.", usuario.Id);
        await _auditoria.RegistrarAsync(AccionesAuditoria.CodigosRecuperacionRegenerados, true, usuario.Id, "Usuario", usuario.Id.ToString());
        return Ok(new CodigosRecuperacionDto(codigos));
    }

    // ---------------------------------------------------------------- auxiliares

    private async Task<IActionResult> ResultadoSegundoFactorAsync(
        Usuario usuario, Microsoft.AspNetCore.Identity.SignInResult resultado, string metodo, bool bloqueadoAntes)
    {
        if (resultado.Succeeded)
        {
            await RegistrarAccesoAsync(usuario);
            await RenovarTokenCsrfAsync(usuario);
            int? restantes = metodo == "recuperacion" ? await _userManager.CountRecoveryCodesAsync(usuario) : null;
            await _auditoria.RegistrarAsync(AccionesAuditoria.SegundoFactorExitoso, true, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { metodo, codigosRecuperacionRestantes = restantes });
            return Ok(await CrearUsuarioActualAsync(usuario, sesionConMfa: true));
        }

        _logger.LogInformation("Segundo factor ({Metodo}) fallido para el usuario {UsuarioId}. Bloqueado: {Bloqueado}.",
            metodo, usuario.Id, resultado.IsLockedOut);
        await _auditoria.RegistrarAsync(AccionesAuditoria.SegundoFactorFallido, false, usuario.Id, "Usuario", usuario.Id.ToString(),
            new { metodo, bloqueado = resultado.IsLockedOut, inactivo = resultado.IsNotAllowed });
        await AuditarBloqueoSiOcurrioAsync(usuario, bloqueadoAntes, metodo);
        if (resultado.IsLockedOut || resultado.IsNotAllowed)
        {
            await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        }
        return CodigoFallido();
    }

    /// <summary>
    /// SignInAsync no cambia HttpContext.User en la petición en curso; se reemplaza para que el
    /// token antiforgery nuevo quede ligado al usuario que acaba de entrar (o a nadie tras el logout).
    /// </summary>
    private async Task RenovarTokenCsrfAsync(Usuario? usuario)
    {
        HttpContext.User = usuario is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : await _signInManager.CreateUserPrincipalAsync(usuario);
        ConfiguracionAntiforgery.EmitirTokens(HttpContext);
    }

    /// <summary>Registra el momento exacto en que la cuenta pasó a bloqueada.</summary>
    private async Task AuditarBloqueoSiOcurrioAsync(Usuario usuario, bool bloqueadoAntes, string paso)
    {
        if (!bloqueadoAntes && await _userManager.IsLockedOutAsync(usuario))
        {
            var hasta = await _userManager.GetLockoutEndDateAsync(usuario);
            await _auditoria.RegistrarAsync(AccionesAuditoria.CuentaBloqueada, true, usuario.Id, "Usuario", usuario.Id.ToString(),
                new { paso, bloqueadoHastaUtc = hasta?.UtcDateTime });
        }
    }

    private Guid? IdActual() => Guid.TryParse(_userManager.GetUserId(User), out var id) ? id : null;

    private async Task RegistrarAccesoAsync(Usuario usuario)
    {
        usuario.UltimoAcceso = _reloj.GetUtcNow().UtcDateTime;
        await _userManager.UpdateAsync(usuario);
    }

    private async Task<UsuarioActualDto> CrearUsuarioActualAsync(Usuario usuario, bool sesionConMfa)
    {
        var roles = (await _userManager.GetRolesAsync(usuario)).ToList();
        return new UsuarioActualDto(usuario.Id, usuario.Email!, usuario.NombreCompleto, roles,
            usuario.TwoFactorEnabled, sesionConMfa, usuario.DebeCambiarPassword);
    }

    private ObjectResult LimiteExcedido(int segundos)
    {
        Response.Headers.RetryAfter = segundos.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Problemas.Crear(HttpContext, StatusCodes.Status429TooManyRequests, "Demasiadas solicitudes",
            $"Demasiados intentos. Intente de nuevo en {segundos} segundos.");
    }

    private ObjectResult LoginFallido() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autenticado", detail: MensajeLoginFallido);

    private ObjectResult CodigoFallido() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autenticado", detail: MensajeCodigoFallido);

    private ObjectResult Ya2faConfigurado() =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "2FA ya configurado",
            detail: "La verificación en dos pasos ya está activa. Si perdió el acceso, pida a un administrador que la reinicie.");

    private ObjectResult ErroresIdentity(IdentityResult resultado, string campo) =>
        Problemas.Validacion(HttpContext, new Dictionary<string, string[]>
        {
            [campo] = resultado.Errors.Select(e => e.Description).ToArray()
        });

    private static string NormalizarCodigoTotp(string codigo) =>
        new string(codigo.Where(char.IsDigit).ToArray());

    private static string FormatearClave(string clave)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < clave.Length; i += 4)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(clave.AsSpan(i, Math.Min(4, clave.Length - i)));
        }
        return sb.ToString().ToLowerInvariant();
    }

    private string CrearUriOtpauth(string email, string clave)
    {
        var emisor = Uri.EscapeDataString(_seguridad.Emisor2fa);
        return $"otpauth://totp/{emisor}:{Uri.EscapeDataString(email)}?secret={clave}&issuer={emisor}&digits=6&period=30";
    }
}
