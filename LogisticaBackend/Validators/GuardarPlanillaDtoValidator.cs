using FluentValidation;
using LogisticaBackend.Configuration;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using Microsoft.Extensions.Options;

namespace LogisticaBackend.Validators;

public class GuardarPlanillaDtoValidator : AbstractValidator<GuardarPlanillaDto>
{
    public GuardarPlanillaDtoValidator(IOptions<LiquidacionesOptions> opciones, TimeProvider reloj)
    {
        var tope = opciones.Value.TopeValorMonetario;
        var maxRenglones = opciones.Value.MaximoRenglonesGasto;
        var topeTexto = tope.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));

        RuleFor(x => x.Placa)
            .NotEmpty().WithMessage("La placa del vehículo es obligatoria.")
            .MaximumLength(10).WithMessage("La placa no puede exceder 10 caracteres.")
            .Must(Placa.EsValida).WithMessage("La placa debe tener 3 letras y 3 números (p. ej. WCN410).");

        RuleFor(x => x.Conductor)
            .NotEmpty().WithMessage("El nombre del conductor es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre del conductor no puede exceder 150 caracteres.");

        RuleFor(x => x.Origen)
            .NotEmpty().WithMessage("La ciudad de origen es obligatoria.")
            .MaximumLength(100).WithMessage("El origen no puede exceder 100 caracteres.");

        RuleFor(x => x.Destino)
            .NotEmpty().WithMessage("La ciudad de destino es obligatoria.")
            .MaximumLength(100).WithMessage("El destino no puede exceder 100 caracteres.");

        RuleFor(x => x.Manifiesto)
            .MaximumLength(50).WithMessage("El manifiesto no puede exceder 50 caracteres.");

        RuleFor(x => x.EmpresaTransportadora)
            .MaximumLength(150).WithMessage("La empresa transportadora no puede exceder 150 caracteres.");

        RuleFor(x => x.FechaInicial)
            .Must(f => f <= FechaNegocio.Hoy(reloj)).WithMessage("La fecha inicial no puede ser futura.");

        RuleFor(x => x.FechaFinal)
            .GreaterThanOrEqualTo(x => x.FechaInicial)
            .WithMessage("La fecha final del viaje no puede ser anterior a la fecha inicial.")
            .Must(f => f <= FechaNegocio.Hoy(reloj)).WithMessage("La fecha final no puede ser futura.");

        RuleFor(x => x.Flete).InclusiveBetween(0m, tope)
            .WithMessage($"El flete debe estar entre 0 y {topeTexto}.");
        RuleFor(x => x.Anticipo).InclusiveBetween(0m, tope)
            .WithMessage($"El anticipo debe estar entre 0 y {topeTexto}.");
        RuleFor(x => x.SaldoAnterior).InclusiveBetween(0m, tope)
            .WithMessage($"El saldo anterior debe estar entre 0 y {topeTexto}.");

        RuleFor(x => x.Gastos)
            .NotNull().WithMessage("La lista de gastos es obligatoria.")
            .Must(g => g is null || g.Count <= maxRenglones)
            .WithMessage($"Una planilla admite máximo {maxRenglones} renglones de gasto.");

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

            gasto.RuleFor(g => g.Valor).InclusiveBetween(0m, tope)
                .WithMessage($"El valor del gasto debe estar entre 0 y {topeTexto}.");
        });
    }
}

public class ConsultaPlanillasDtoValidator : AbstractValidator<ConsultaPlanillasDto>
{
    public ConsultaPlanillasDtoValidator()
    {
        RuleFor(x => x.Pagina).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(x => x.Tamano).InclusiveBetween(1, ConsultaPlanillasDto.TamanoMaximo)
            .WithMessage($"El tamaño de página debe estar entre 1 y {ConsultaPlanillasDto.TamanoMaximo}.");
        RuleFor(x => x.Hasta)
            .Must((dto, hasta) => dto.Desde is null || hasta is null || hasta >= dto.Desde)
            .WithMessage("La fecha final debe ser igual o posterior a la inicial.");
        RuleFor(x => x.Placa).MaximumLength(10).WithMessage("La placa no puede exceder 10 caracteres.");
        RuleFor(x => x.Conductor).MaximumLength(150).WithMessage("El conductor no puede exceder 150 caracteres.");
    }
}
