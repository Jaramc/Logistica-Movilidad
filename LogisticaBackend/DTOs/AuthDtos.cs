namespace LogisticaBackend.DTOs;

public sealed record LoginDto(string Email, string Password);

/// <summary>Código TOTP de 6 dígitos o código de recuperación.</summary>
public sealed record CodigoDto(string Codigo);

public sealed record CambiarPasswordDto(string PasswordActual, string PasswordNueva);

/// <summary>
/// Resultado del primer paso del login. Si <see cref="Requiere2fa"/> es true, el cliente
/// debe llamar a login-2fa o login-recuperacion. Si no, la sesión quedó abierta pero
/// limitada hasta configurar el 2FA (y cambiar la contraseña si aplica).
/// </summary>
public sealed record LoginRespuestaDto(bool Requiere2fa);

public sealed record UsuarioActualDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles,
    bool Tiene2fa,
    bool SesionConMfa,
    bool DebeCambiarPassword);

public sealed record Configurar2faRespuestaDto(string Clave, string UriOtpauth);

public sealed record CodigosRecuperacionDto(IReadOnlyList<string> Codigos);
