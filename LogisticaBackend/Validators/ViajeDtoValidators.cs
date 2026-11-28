using FluentValidation;
using Microsoft.Extensions.Options;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Entities;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Validators;

public class AbrirViajeDtoValidator : AbstractValidator<AbrirViajeDto>
{
    public AbrirViajeDtoValidator(IOptions<LiquidacionesOptions> opciones, TimeProvider reloj)
    {
        var tope = opciones.Value.TopeValorMonetario;
        RuleFor(x => x.ConductorId).GreaterThan(0).WithMessage("Debe elegir un conductor.");
        RuleFor(x => x.VehiculoId).GreaterThan(0).WithMessage("Debe elegir un vehículo.");
        RuleFor(x => x.Origen).NotEmpty().MaximumLength(100).WithMessage("El origen es obligatorio (máximo 100 caracteres).");
        RuleFor(x => x.Destino).NotEmpty().MaximumLength(100).WithMessage("El destino es obligatorio (máximo 100 caracteres).");
        RuleFor(x => x.Cliente).NotEmpty().MaximumLength(150).WithMessage("El cliente es obligatorio (máximo 150 caracteres).");
        RuleFor(x => x.Manifiesto).MaximumLength(50);
        RuleFor(x => x.Flete).InclusiveBetween(0m, tope).WithMessage($"El flete debe estar entre 0 y {tope:N0}.");
        RuleFor(x => x.FechaInicio).Must(f => f <= DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))
            .WithMessage("La fecha de inicio no puede ser futura.");
    }
}

public class RegistrarTrasladoDtoValidator : AbstractValidator<RegistrarTrasladoDto>
{
    public RegistrarTrasladoDtoValidator(IOptions<LiquidacionesOptions> opciones, TimeProvider reloj)
    {
        var tope = opciones.Value.TopeValorMonetario;
        RuleFor(x => x.Valor).GreaterThan(0m).LessThanOrEqualTo(tope).WithMessage($"El valor debe ser mayor que 0 y hasta {tope:N0}.");
        RuleFor(x => x.Medio).Must(m => Enum.TryParse<MedioTraslado>(m, out _)).WithMessage("El medio debe ser Transferencia, Efectivo o Giro.");
        RuleFor(x => x.Fecha).Must(f => f <= DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime))
            .WithMessage("La fecha no puede ser futura.");
        RuleFor(x => x.Nota).MaximumLength(200);
    }
}

public class ConsultaViajesDtoValidator : AbstractValidator<ConsultaViajesDto>
{
    public ConsultaViajesDtoValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Tamano).InclusiveBetween(1, ConsultaViajesDto.TamanoMaximo);
        RuleFor(x => x.Estado).Must(e => string.IsNullOrWhiteSpace(e) || Enum.TryParse<EstadoViaje>(e, out _))
            .WithMessage("El estado debe ser Abierto, EnRevision, Aprobado o Saldado.");
        RuleFor(x => x.Hasta).Must((dto, hasta) => dto.Desde is null || hasta is null || hasta >= dto.Desde)
            .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
    }
}
