namespace LogisticaBackend.DTOs;

public sealed record UsuarioDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    string? Rol,
    bool Activo,
    bool Tiene2fa,
    bool DebeCambiarPassword,
    bool Bloqueado,
    DateTime CreadoEn,
    DateTime? UltimoAcceso,
    /// <summary>Aún no configura el 2FA.</summary>
    bool PendienteActivacion,
    DateTime? PasswordTemporalExpiraEn,
    /// <summary>La contraseña temporal venció o ya se usó: hay que generar otra.</summary>
    bool PasswordTemporalInutilizable);

public sealed record CrearUsuarioDto(string Email, string NombreCompleto, string Rol);

/// <summary>La contraseña temporal se muestra una sola vez; no se puede volver a consultar.</summary>
public sealed record UsuarioCreadoDto(UsuarioDto Usuario, string PasswordTemporal);

public sealed record CambiarRolDto(string Rol);

public sealed record CambiarEstadoDto(bool Activo);
