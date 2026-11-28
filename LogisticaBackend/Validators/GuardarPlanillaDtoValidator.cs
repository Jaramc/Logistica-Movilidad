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
        var topeSaldo = opciones.Value.TopeSaldo;
        var topeSaldoTexto = topeSaldo.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        var maxRenglones = opciones.Value.MaximoRenglonesGasto;
        var topeTexto = tope.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));

        RuleFor(x => x.ViajeId)
            .GreaterThan(0).WithMessage("Debe indicar el viaje al que pertenece la planilla.");

        // Lo que leyó el OCR, solo para comparar con la placa del viaje (advertencia, no error):
        // no es obligatoria porque el revisor puede guardar aunque el OCR no la haya encontrado.
        RuleFor(x => x.Placa)
            .MaximumLength(10).WithMessage("La placa no puede exceder 10 caracteres.")
            .Must(p => string.IsNullOrWhiteSpace(p) || Placa.EsValida(p))
            .WithMessage("La placa debe tener 3 letras y 3 números (p. ej. WCN410).");

        RuleFor(x => x.Origen)
            .NotEmpty().WithMessage("La ciudad de origen es obligatoria.")
            .MaximumLength(100).WithMessage("El origen no puede exceder 100 caracteres.");

        RuleFor(x => x.Destino)
            .NotEmpty().WithMessage("La ciudad de destino es obligatoria.")
            .MaximumLength(100).WithMessage("El destino no puede exceder 100 caracteres.");

        RuleFor(x => x.Manifiesto)
            .MaximumLength(50).WithMessage("El manifiesto no puede exceder 50 caracteres.");

        RuleFor(x => x.EmpresaTransportadora)
            .MaximumLength(150).WithMessage("La empresa transportadora no puede exceder 150 caracteres.")
            .Must(e => string.IsNullOrWhiteSpace(e) || TextoPlanilla.ContarLetras(e) >= 2)
            .WithMessage("La empresa debe tener letras.");

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

        RuleFor(x => x.TotalGastosDeclarado).InclusiveBetween(0m, topeSaldo)
            .When(x => x.TotalGastosDeclarado is not null)
            .WithMessage($"El total de gastos declarado debe estar entre 0 y {topeSaldoTexto}.");
        RuleFor(x => x.SaldoViajeDeclarado).InclusiveBetween(-topeSaldo, topeSaldo)
            .When(x => x.SaldoViajeDeclarado is not null).WithMessage($"El saldo declarado debe estar entre ±{topeSaldoTexto}.");
        RuleFor(x => x.SaldoAnteriorDeclarado).InclusiveBetween(-topeSaldo, topeSaldo)
            .When(x => x.SaldoAnteriorDeclarado is not null).WithMessage($"El saldo declarado debe estar entre ±{topeSaldoTexto}.");
        RuleFor(x => x.SaldoNuevoDeclarado).InclusiveBetween(-topeSaldo, topeSaldo)
            .When(x => x.SaldoNuevoDeclarado is not null).WithMessage($"El saldo declarado debe estar entre ±{topeSaldoTexto}.");

        RuleFor(x => x.Gastos)
            .NotNull().WithMessage("La lista de gastos es obligatoria.")
            .Must(g => g is null || g.Count >= 1)
            .WithMessage("La planilla debe tener al menos un renglón de gasto.")
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
                .WithMessage("No se permite incluir 'FLETE' o 'ANTICIPO' como renglones de gasto (deben registrarse exclusivamente en sus campos dedicados).")
                .Must(concepto => !ListaNegraConceptos.Coincide(concepto))
                .WithMessage(g => $"«{g.Concepto}» es un dato de cabecera o de saldos (empresa, fechas, placa, total, saldo...), no un gasto.");
            gasto.RuleFor(g => g.Etiqueta)
                .Must(e => !ListaNegraConceptos.Coincide(e))
                .WithMessage(g => $"«{g.Etiqueta}» es un dato de cabecera o de saldos, no un gasto.");
            gasto.RuleFor(g => g.Descripcion)
                .Must(d => !ListaNegraConceptos.Coincide(d))
                .WithMessage(g => $"«{g.Descripcion}» es un dato de cabecera o de saldos, no un gasto.");

            gasto.RuleFor(g => g.Categoria)
                .Must(c => ConceptoNormalizer.CategoriasCanonicas.Contains(c!.Trim().ToUpperInvariant()))
                .When(g => !string.IsNullOrWhiteSpace(g.Categoria))
                .WithMessage("La categoría debe ser una de las 16 del consolidado.");
            gasto.RuleFor(g => g.Etiqueta).MaximumLength(60).WithMessage("La etiqueta no puede exceder 60 caracteres.");
            gasto.RuleFor(g => g.Descripcion).MaximumLength(200).WithMessage("La descripción no puede exceder 200 caracteres.");

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
