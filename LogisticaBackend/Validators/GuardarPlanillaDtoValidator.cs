using FluentValidation;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

public class GuardarPlanillaDtoValidator : AbstractValidator<GuardarPlanillaDto>
{
    public GuardarPlanillaDtoValidator()
    {
        RuleFor(x => x.Placa)
            .NotEmpty().WithMessage("La placa del vehículo es obligatoria.")
            .MinimumLength(3).WithMessage("La placa debe contener al menos 3 caracteres.")
            .MaximumLength(10).WithMessage("La placa no puede exceder 10 caracteres.");

        RuleFor(x => x.Conductor)
            .NotEmpty().WithMessage("El nombre del conductor es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre del conductor no puede exceder 150 caracteres.");

        RuleFor(x => x.Origen)
            .NotEmpty().WithMessage("La ciudad de origen es obligatoria.")
            .MaximumLength(100).WithMessage("El origen no puede exceder 100 caracteres.");

        RuleFor(x => x.Destino)
            .NotEmpty().WithMessage("La ciudad de destino es obligatoria.")
            .MaximumLength(100).WithMessage("El destino no puede exceder 100 caracteres.");

        RuleFor(x => x.FechaFinal)
            .GreaterThanOrEqualTo(x => x.FechaInicial)
            .WithMessage("La fecha final del viaje no puede ser anterior a la fecha inicial.");

        RuleFor(x => x.Flete)
            .GreaterThanOrEqualTo(0m).WithMessage("El flete debe ser un valor mayor o igual a 0.");

        RuleFor(x => x.Anticipo)
            .GreaterThanOrEqualTo(0m).WithMessage("El anticipo debe ser un valor mayor o igual a 0.");

        RuleForEach(x => x.Gastos).ChildRules(gasto =>
        {
            gasto.RuleFor(g => g.Concepto)
                .NotEmpty().WithMessage("El concepto del gasto es obligatorio.")
                .MaximumLength(150).WithMessage("El concepto no puede exceder 150 caracteres.")
                .Must(concepto =>
                {
                    var c = concepto?.Trim().ToUpperInvariant() ?? string.Empty;
                    return !c.Contains("FLETE") && !c.Contains("ANTICIPO");
                })
                .WithMessage("No se permite incluir 'FLETE' o 'ANTICIPO' como renglones de gasto (deben registrarse exclusivamente en sus campos dedicados).");

            gasto.RuleFor(g => g.Valor)
                .GreaterThanOrEqualTo(0m).WithMessage("El valor del gasto debe ser mayor o igual a 0.");
        });
    }
}

