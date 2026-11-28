using FluentValidation;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

public class ConsultaAuditoriaDtoValidator : AbstractValidator<ConsultaAuditoriaDto>
{
    public ConsultaAuditoriaDtoValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.Tamano).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
        RuleFor(x => x.Accion).MaximumLength(50).WithMessage("La acción no puede exceder 50 caracteres.");
        RuleFor(x => x.Hasta)
            .Must((dto, hasta) => dto.Desde is null || hasta is null || hasta >= dto.Desde)
            .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
    }
}
