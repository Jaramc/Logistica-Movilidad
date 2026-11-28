using Microsoft.AspNetCore.Identity;

namespace LogisticaBackend.Auth;

/// <summary>Mensajes de Identity en español. No revelan si un correo existe salvo al administrador.</summary>
public class IdentityErrorDescriberEs : IdentityErrorDescriber
{
    private static IdentityError E(string code, string descripcion) => new() { Code = code, Description = descripcion };

    public override IdentityError DefaultError() => E(nameof(DefaultError), "Ocurrió un error. Intente de nuevo.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "El registro fue modificado por otra persona. Recargue e intente de nuevo.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "La contraseña actual no es correcta.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "El código no es válido.");
    public override IdentityError RecoveryCodeRedemptionFailed() => E(nameof(RecoveryCodeRedemptionFailed), "El código de recuperación no es válido.");
    public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Este inicio de sesión ya está asociado a otra cuenta.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), "El nombre de usuario no es válido.");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), "El correo no es válido.");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), "Ya existe un usuario con ese correo.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), "Ya existe un usuario con ese correo.");
    public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), "El rol no es válido.");
    public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), "El rol ya existe.");
    public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "El usuario ya tiene una contraseña.");
    public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para este usuario.");
    public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), "El usuario ya tiene ese rol.");
    public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), "El usuario no tiene ese rol.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"La contraseña debe tener al menos {uniqueChars} caracteres distintos.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un símbolo.");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una minúscula.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una mayúscula.");
}
