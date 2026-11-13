using Shouldly;
using LogisticaBackend.Configuration;
using LogisticaBackend.DTOs;
using LogisticaBackend.Tests.Soporte;
using LogisticaBackend.Validators;
using Xunit;

namespace LogisticaBackend.Tests;

public class GuardarPlanillaDtoValidatorTests
{
    private readonly GuardarPlanillaDtoValidator _validator = new(
        Microsoft.Extensions.Options.Options.Create(new LiquidacionesOptions()),
        new RelojFijo(new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero)));

    private static GuardarPlanillaDto CrearDtoValido() => new(
        Placa: "WCN410",
        Conductor: "PEDRO PEREZ",
        Origen: "BOGOTA",
        Destino: "MEDELLIN",
        FechaInicial: new DateOnly(2026, 11, 1),
        FechaFinal: new DateOnly(2026, 11, 5),
        Flete: 6976800m,
        Anticipo: 2800000m,
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

    [Theory]
    [InlineData("WCN410", true)]
    [InlineData("wcn 410", true)]   // se normaliza
    [InlineData("WCN-410", true)]
    [InlineData("WC4100", false)]
    [InlineData("WCNA10", false)]
    [InlineData("WCN41", false)]
    public void Validar_FormatoDePlaca(string placa, bool esperado)
    {
        _validator.Validate(CrearDtoValido() with { Placa = placa }).IsValid.ShouldBe(esperado);
    }

    [Fact]
    public void Validar_FechaFutura_EsRechazada()
    {
        // El reloj de prueba está en 2026-11-13 (hora de Colombia).
        var dto = CrearDtoValido() with { FechaInicial = new DateOnly(2026, 11, 14), FechaFinal = new DateOnly(2026, 11, 15) };
        var resultado = _validator.Validate(dto);
        resultado.Errors.ShouldContain(e => e.ErrorMessage.Contains("futura"));
    }

    [Fact]
    public void Validar_ValorSobreElTope_EsRechazado()
    {
        var dto = CrearDtoValido() with { Gastos = new List<GastoItemDto> { new("ACPM", 200_000_001m) } };
        _validator.Validate(dto).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validar_GastoSobreTopeDe20Millones_EsRechazado()
    {
        var dto = CrearDtoValido() with { Gastos = new List<GastoItemDto> { new("ACPM", 20_000_001m) } };
        _validator.Validate(dto).IsValid.ShouldBeFalse();
        (CrearDtoValido() with { Gastos = new List<GastoItemDto> { new("ACPM", 20_000_000m) } }).ShouldSatisfyAllConditions(
            d => _validator.Validate(d).IsValid.ShouldBeTrue());
    }

    [Fact]
    public void Validar_FleteOAnticipoSobreTope_SonRechazados()
    {
        _validator.Validate(CrearDtoValido() with { Flete = 20_000_001m }).IsValid.ShouldBeFalse();
        _validator.Validate(CrearDtoValido() with { Anticipo = 20_000_001m }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validar_TextoDemasiadoLargo_EsRechazado()
    {
        _validator.Validate(CrearDtoValido() with { Manifiesto = new string('X', 51) }).IsValid.ShouldBeFalse();
    }
}
