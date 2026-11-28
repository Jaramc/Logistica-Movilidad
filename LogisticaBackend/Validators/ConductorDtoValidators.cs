using FluentValidation;
using Microsoft.Extensions.Options;
using LogisticaBackend.Configuration;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

public class CrearConductorDtoValidator : AbstractValidator<CrearConductorDto>
{
    public CrearConductorDtoValidator()
    {
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
        RuleFor(x => x.Cedula).NotEmpty().WithMessage("La cédula es obligatoria.")
            .Matches("^[0-9]{6,12}$").WithMessage("La cédula debe tener entre 6 y 12 dígitos, solo números.");
    }
}

public class ActualizarConductorDtoValidator : AbstractValidator<ActualizarConductorDto>
{
    public ActualizarConductorDtoValidator()
    {
        RuleFor(x => x.NombreCompleto).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
    }
}

public class SaldoInicialDtoValidator : AbstractValidator<SaldoInicialDto>
{
    public SaldoInicialDtoValidator(IOptions<LiquidacionesOptions> opciones)
    {
        var tope = opciones.Value.TopeSaldo;
        RuleFor(x => x.Valor).InclusiveBetween(-tope, tope).WithMessage($"El saldo inicial debe estar entre ±{tope:N0}.");
        RuleFor(x => x.Justificacion).NotEmpty().WithMessage("La justificación es obligatoria.")
            .MaximumLength(300).WithMessage("La justificación no puede exceder 300 caracteres.");
    }
}

public class RegistrarPagoDtoValidator : AbstractValidator<RegistrarPagoDto>
{
    public RegistrarPagoDtoValidator(IOptions<LiquidacionesOptions> opciones)
    {
        var tope = opciones.Value.TopeValorMonetario;
        RuleFor(x => x.Tipo).Must(t => t is "DescuentoNomina" or "PagoEmpresa")
            .WithMessage("El tipo debe ser DescuentoNomina o PagoEmpresa.");
        RuleFor(x => x.Valor).GreaterThan(0m).LessThanOrEqualTo(tope)
            .WithMessage($"El valor debe ser mayor que 0 y hasta {tope:N0}.");
        RuleFor(x => x.Quincena).MaximumLength(20);
        RuleFor(x => x.Medio).MaximumLength(30);
        RuleFor(x => x.Nota).MaximumLength(200);
    }
}

public class RegistrarAjusteDtoValidator : AbstractValidator<RegistrarAjusteDto>
{
    public RegistrarAjusteDtoValidator(IOptions<LiquidacionesOptions> opciones)
    {
        var tope = opciones.Value.TopeSaldo;
        RuleFor(x => x.Valor).InclusiveBetween(-tope, tope).WithMessage($"El ajuste debe estar entre ±{tope:N0}.");
        RuleFor(x => x.Justificacion).NotEmpty().WithMessage("La justificación es obligatoria.")
            .MaximumLength(300).WithMessage("La justificación no puede exceder 300 caracteres.");
    }
}

public class CrearVehiculoDtoValidator : AbstractValidator<CrearVehiculoDto>
{
    public CrearVehiculoDtoValidator()
    {
        RuleFor(x => x.Placa).NotEmpty().WithMessage("La placa es obligatoria.")
            .Must(p => Domain.Services.Placa.EsValida(p)).WithMessage("La placa debe tener 3 letras y 3 números (p. ej. WCN410).");
        RuleFor(x => x.Tipo).MaximumLength(30).WithMessage("El tipo no puede exceder 30 caracteres.");
    }
}
