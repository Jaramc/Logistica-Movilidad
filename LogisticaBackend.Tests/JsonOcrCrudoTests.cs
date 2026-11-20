using LogisticaBackend.Domain.Services;
using Shouldly;
using Xunit;

namespace LogisticaBackend.Tests;

public class JsonOcrCrudoTests
{
    [Fact]
    public void Sanear_JsonValido_LoConserva()
    {
        const string json = """[{"nombre":"Placa","content":"WCN410","confidence":0.9}]""";
        JsonOcrCrudo.Sanear(json).ShouldBe((json, JsonOcrCrudo.Resultado.Aceptado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Sanear_Vacio_DevuelveNullSinWarning(string? json)
    {
        JsonOcrCrudo.Sanear(json).ShouldBe((null, JsonOcrCrudo.Resultado.Vacio));
    }

    [Theory]
    [InlineData("{no es json")]
    [InlineData("[1,2")]
    [InlineData("""{"x":"\u0000"}""")]
    public void Sanear_JsonInvalidoOQueJsonbRechaza_DevuelveNull(string json)
    {
        JsonOcrCrudo.Sanear(json).ShouldBe((null, JsonOcrCrudo.Resultado.Invalido));
    }

    [Fact]
    public void Sanear_MasDe256KB_DevuelveNull()
    {
        var json = "\"" + new string('a', JsonOcrCrudo.TamanoMaximoBytes) + "\"";
        JsonOcrCrudo.Sanear(json).ShouldBe((null, JsonOcrCrudo.Resultado.DemasiadoGrande));
    }

    [Fact]
    public void Sanear_JustoEnElTope_LoConserva()
    {
        var json = "\"" + new string('a', JsonOcrCrudo.TamanoMaximoBytes - 2) + "\"";
        JsonOcrCrudo.Sanear(json).Resultado.ShouldBe(JsonOcrCrudo.Resultado.Aceptado);
    }
}
