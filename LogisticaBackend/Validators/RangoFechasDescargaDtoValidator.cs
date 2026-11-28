using FluentValidation;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

public class RangoFechasDescargaDtoValidator : AbstractValidator<RangoFechasDescargaDto>
{
    public RangoFechasDescargaDtoValidator(TimeProvider reloj)
    {
        RuleFor(x => x.Desde).NotNull().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.Hasta).NotNull().WithMessage("La fecha final es obligatoria.");

        When(x => x.Desde.HasValue && x.Hasta.HasValue, () =>
        {
            RuleFor(x => x.Hasta)
                .Must((dto, hasta) => hasta >= dto.Desde)
                .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
            RuleFor(x => x.Hasta)
                .Must((dto, hasta) => hasta <= dto.Desde!.Value.AddYears(1))
                .WithMessage("El rango máximo por descarga es de 1 año.");
            RuleFor(x => x.Desde)
                .Must(desde => desde <= FechaNegocio.Hoy(reloj))
                .WithMessage("La fecha inicial no puede ser futura.");
        });
    }
}
