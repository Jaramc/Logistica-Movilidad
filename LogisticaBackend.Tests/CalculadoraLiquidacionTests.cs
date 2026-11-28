using Shouldly;
using LogisticaBackend.Domain.Services;
using LogisticaBackend.DTOs;
using Xunit;

namespace LogisticaBackend.Tests;

public class CalculadoraLiquidacionTests
{
    [Fact]
    public void CalcularSaldo_PlanillaReal_RetornaTotalesExactos()
    {
        // Caso de oro basado en planilla real:
        // Flete: 6.976.800, Anticipo: 2.800.000, Gastos: 2.869.200
        // Saldo = 2.869.200 - 2.800.000 = 69.200 (BalanceAnticipo)
        var valoresGastos = new List<decimal>
        {
            178000m,  // PARQUEO
            32900m,   // Papeleria y pasajes
            1500000m, // PEAJES
            1158300m  // ACPM
        };

        var resultado = CalculadoraLiquidacion.Calcular(
            flete: 6976800m,
            anticipo: 2800000m,
            gastos: valoresGastos,
            saldoAnterior: 0m,
            formula: FormulaSaldoViaje.BalanceAnticipo
        );

        resultado.TotalGastos.ShouldBe(2869200m);
        resultado.SaldoViaje.ShouldBe(69200m);
        resultado.SaldoTotal.ShouldBe(69200m);
    }

    [Fact]
    public void CalcularSaldo_FormulaLiquidacionFlete_CalculaDiferenciaCorrecta()
    {
        // LiquidacionFlete: Saldo = Flete - Anticipo - TotalGastos
        // 6.000.000 - 2.000.000 - 2.000.000 = 2.000.000
        var valoresGastos = new List<decimal>
        {
            500000m,
            1500000m
        };

        var resultado = CalculadoraLiquidacion.Calcular(
            flete: 6000000m,
            anticipo: 2000000m,
            gastos: valoresGastos,
            saldoAnterior: 100000m,
            formula: FormulaSaldoViaje.LiquidacionFlete
        );

        resultado.TotalGastos.ShouldBe(2000000m);
        resultado.SaldoViaje.ShouldBe(2000000m);
        resultado.SaldoTotal.ShouldBe(2100000m); // 2.000.000 + 100.000
    }

    [Fact]
    public void CalcularSaldo_ConSaldoAnteriorNegativo_RestaAlSaldoTotal()
    {
        var valoresGastos = new List<decimal>
        {
            1000000m
        };

        var resultado = CalculadoraLiquidacion.Calcular(
            flete: 3000000m,
            anticipo: 1000000m,
            gastos: valoresGastos,
            saldoAnterior: -50000m,
            formula: FormulaSaldoViaje.BalanceAnticipo
        );

        resultado.SaldoViaje.ShouldBe(0m); // 1.000.000 - 1.000.000
        resultado.SaldoTotal.ShouldBe(-50000m);
    }

    [Fact]
    public void RedondeoFinanciero_UsaMidpointAwayFromZero()
    {
        CalculadoraLiquidacion.Redondear(12.505m).ShouldBe(12.51m);
        CalculadoraLiquidacion.Redondear(12.504m).ShouldBe(12.50m);
        CalculadoraLiquidacion.Redondear(100.005m).ShouldBe(100.01m);
        CalculadoraLiquidacion.Redondear(50.005m).ShouldBe(50.01m);
    }
}
