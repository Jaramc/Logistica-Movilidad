using LogisticaBackend.Domain.Entities;
using LogisticaBackend.Domain.Services;
using Shouldly;
using Xunit;

namespace LogisticaBackend.Tests;

public class CuentaConductorCalculosTests
{
    [Theory]
    [InlineData(TipoPago.PagoEmpresa, 5_700, -5_700)]
    [InlineData(TipoPago.DescuentoNomina, 5_700, 5_700)]
    public void ValorConSigno_SegunElTipo(TipoPago tipo, decimal valor, decimal esperado)
    {
        CuentaConductorCalculos.ValorConSigno(tipo, valor).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(-63_500, 69_200, 5_700)] // Cliente Ejemplo: saldo inicial a favor de la empresa + saldo de viaje a favor del conductor
    [InlineData(0, -5_700, -5_700)]
    [InlineData(100, -100, 0)]
    public void SaldoResultante_SumaConSignoYRedondea(decimal saldoAnterior, decimal valor, decimal esperado)
    {
        CuentaConductorCalculos.SaldoResultante(saldoAnterior, valor).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(2_869_200, 2_800_000, 69_200)] // planilla real de Cliente Ejemplo
    [InlineData(100, 150, -50)]
    public void SaldoViaje_GastosMenosAnticipo(decimal totalGastos, decimal anticipoTraslados, decimal esperado)
    {
        CuentaConductorCalculos.SaldoViaje(totalGastos, anticipoTraslados).ShouldBe(esperado);
    }
}
