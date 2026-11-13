using Shouldly;
using LogisticaBackend.DTOs;
using LogisticaBackend.Validators;
using Xunit;

namespace LogisticaBackend.Tests;

public class GuardarPlanillaDtoValidatorTests
{
    private readonly GuardarPlanillaDtoValidator _validator = new();

    private static GuardarPlanillaDto CrearDtoValido() => new(
        Placa: "WCN410",
        Conductor: "PEDRO PEREZ",
        Origen: "BOGOTA",
        Destino: "MEDELLIN",
        FechaInicial: new DateOnly(2026, 11, 1),
        FechaFinal: new DateOnly(2026, 11, 5),
        Flete: 6976800m,
        Anticipo: 2800000m,
        TotalGastos: 2869200m,
        SaldoViaje: 69200m,
        SaldoAnterior: 0m,
        SaldoTotal: 69200m,
        Gastos: new List<GastoItemDto>
        {
            new("PARQUEO", 178000m),
            new("PEAJES", 1500000m)
        },
        Manifiesto: "MAN-1234",
        EmpresaTransportadora: "LOGISTICA S.A.S."
    );

    [Fact]
    public void Validar_DtoCorrecto_PasaSinErrores()
    {
        var dto = CrearDtoValido();
        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeTrue();
        resultado.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("FLETE")]
    [InlineData("flete viaje")]
    [InlineData("VALOR FLETE")]
    public void Validar_GastoConConceptoFlete_EsRechazado(string conceptoFlete)
    {
        var dto = CrearDtoValido() with
        {
            Gastos = new List<GastoItemDto>
            {
                new(conceptoFlete, 6976800m),
                new("PARQUEO", 178000m)
            }
        };

        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeFalse();
        resultado.Errors.ShouldContain(e => e.PropertyName.StartsWith("Gastos") && e.ErrorMessage.Contains("No se permite incluir 'FLETE' o 'ANTICIPO'"));
    }

    [Theory]
    [InlineData("ANTICIPO")]
    [InlineData("anticipos")]
    [InlineData("ANTICIPO EMPRESA")]
    public void Validar_GastoConConceptoAnticipo_EsRechazado(string conceptoAnticipo)
    {
        var dto = CrearDtoValido() with
        {
            Gastos = new List<GastoItemDto>
            {
                new(conceptoAnticipo, 2800000m),
                new("PEAJES", 100000m)
            }
        };

        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeFalse();
        resultado.Errors.ShouldContain(e => e.PropertyName.StartsWith("Gastos") && e.ErrorMessage.Contains("No se permite incluir 'FLETE' o 'ANTICIPO'"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validar_PlacaInvalida_EsRechazada(string? placaInvalida)
    {
        var dto = CrearDtoValido() with { Placa = placaInvalida! };
        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeFalse();
        resultado.Errors.ShouldContain(e => e.PropertyName == "Placa");
    }

    [Fact]
    public void Validar_FleteNegativo_EsRechazado()
    {
        var dto = CrearDtoValido() with { Flete = -100m };
        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeFalse();
        resultado.Errors.ShouldContain(e => e.PropertyName == "Flete");
    }

    [Fact]
    public void Validar_AnticipoNegativo_EsRechazado()
    {
        var dto = CrearDtoValido() with { Anticipo = -50m };
        var resultado = _validator.Validate(dto);

        resultado.IsValid.ShouldBeFalse();
        resultado.Errors.ShouldContain(e => e.PropertyName == "Anticipo");
    }
}
