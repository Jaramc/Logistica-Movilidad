using LogisticaBackend.Domain.Services;
using Shouldly;
using Xunit;

namespace LogisticaBackend.Tests;

/// <summary>
/// Mismos casos que "parseCOP — casos compartidos con MontoParser" en
/// LogisticaFrontend/src/features/planillas/utils/money.test.ts. Si agregas uno, agrégalo en los dos.
/// </summary>
public class MontoParserTests
{
    [Theory]
    [InlineData("$ 1.500.000", "1500000")]
    [InlineData("1.500.000,50", "1500000.50")]
    [InlineData("1,500,000.50", "1500000.50")]
    [InlineData("12,50", "12.50")]
    [InlineData("12.50", "12.50")]
    [InlineData("12,5", "12.5")]
    [InlineData("150.000", "150000")]
    [InlineData("1,500", "1500")]
    [InlineData("-50.000", "-50000")]
    [InlineData("- 50.000", "-50000")]
    [InlineData("(50.000)", "-50000")]
    [InlineData("-$ 150.000", "-150000")]
    [InlineData("$ -150.000", "-150000")]
    [InlineData("COP -20.000", "-20000")]
    [InlineData("cop 20.000", "20000")]
    [InlineData("COP 2024-11", "202411")]
    [InlineData("50.000-", "50000")]
    [InlineData("(50.000", "50000")]
    [InlineData("", "0")]
    [InlineData("   ", "0")]
    [InlineData(null, "0")]
    [InlineData("abc", "0")]
    [InlineData("-", "0")]
    public void ParseCop_CasosCompartidosConElFrontend(string? entrada, string esperado)
    {
        MontoParser.ParseCop(entrada).ShouldBe(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("COP 2024-11")]
    [InlineData("Peaje 12-34")]
    [InlineData("50.000-")]
    public void ParseCop_GuionQueNoEstaAlInicio_NoVuelveNegativo(string entrada)
    {
        MontoParser.ParseCop(entrada).ShouldBeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void ParseCop_RedondeaADosDecimalesAwayFromZero()
    {
        MontoParser.ParseCop("1,005.125").ShouldBe(1005.13m);
    }
}
