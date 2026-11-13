using FluentValidation;
using LogisticaBackend.Auth;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

internal static class LimitesTexto
{
    public const int Email = 254;
    public const int Password = 128;
    public const int Codigo = 32;
    public const int Nombre = 150;
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        // Mensajes neutros: no se informa nada sobre la existencia del usuario.
        RuleFor(x => x.Email).NotEmpty().WithMessage("Ingrese el correo.")
            .MaximumLength(LimitesTexto.Email).WithMessage("El correo es demasiado largo.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Ingrese la contraseña.")
            .MaximumLength(LimitesTexto.Password).WithMessage("La contraseña es demasiado larga.");
    }
}

public class CodigoDtoValidator : AbstractValidator<CodigoDto>
{
    public CodigoDtoValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().WithMessage("Ingrese el código.")
            .MaximumLength(LimitesTexto.Codigo).WithMessage("El código es demasiado largo.");
    }
}

public class CambiarPasswordDtoValidator : AbstractValidator<CambiarPasswordDto>
{
    public CambiarPasswordDtoValidator()
    {
        RuleFor(x => x.PasswordActual).NotEmpty().WithMessage("Ingrese la contraseña actual.")
            .MaximumLength(LimitesTexto.Password).WithMessage("La contraseña es demasiado larga.");
        RuleFor(x => x.PasswordNueva).NotEmpty().WithMessage("Ingrese la contraseña nueva.")
            .MaximumLength(LimitesTexto.Password).WithMessage("La contraseña es demasiado larga.")
            .NotEqual(x => x.PasswordActual).WithMessage("La contraseña nueva debe ser distinta de la actual.");
    }
}

public class CrearUsuarioDtoValidator : AbstractValidator<CrearUsuarioDto>
{
    public CrearUsuarioDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(LimitesTexto.Email).WithMessage("El correo es demasiado largo.")
            .EmailAddress().WithMessage("El correo no es válido.");
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(LimitesTexto.Nombre).WithMessage($"El nombre admite máximo {LimitesTexto.Nombre} caracteres.");
        RuleFor(x => x.Rol).Must(Roles.EsValido).WithMessage("El rol no es válido.");
    }
}

public class CambiarRolDtoValidator : AbstractValidator<CambiarRolDto>
{
    public CambiarRolDtoValidator()
    {
        RuleFor(x => x.Rol).Must(Roles.EsValido).WithMessage("El rol no es válido.");
    }
}
