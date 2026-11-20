namespace LogisticaBackend.Domain.Services;

public enum FormulaSaldoViaje
{
    /// <summary>
    /// Fórmula por defecto (Balance de Anticipo del Conductor):
    /// Saldo Viaje = Total Gastos - Anticipo.
    /// Si gastó más del anticipo entregado, el saldo es positivo (la empresa debe reembolsarle dinero).
    /// Si gastó menos, es negativo (el conductor debe reintegrar sobrante).
    /// </summary>
    // TODO(negocio): confirmar con el cliente la fórmula oficial del saldo (BalanceAnticipo vs LiquidacionFlete).
    BalanceAnticipo,

    /// <summary>
    /// Fórmula alternativa (Liquidación contra Flete de Viaje):
    /// Saldo Viaje = Flete - Anticipo - Total Gastos.
    /// </summary>
    LiquidacionFlete
}

public record ResultadoCalculo(
    decimal TotalGastos,
    decimal SaldoViaje,
    decimal SaldoTotal
);

/// <summary>
/// Servicio de dominio puro para el cálculo y validación de liquidaciones financieras en moneda colombiana (COP).
/// Es la máxima autoridad contable del backend.
/// </summary>
public static class CalculadoraLiquidacion
{
    /// <summary>
    /// Calcula de manera determinista los totales contables de una planilla.
    /// Redondeo estricto MidpointRounding.AwayFromZero a dos decimales.
    /// </summary>
    public static ResultadoCalculo Calcular(
        decimal flete,
        decimal anticipo,
        IEnumerable<decimal> gastos,
        decimal saldoAnterior = 0m,
        FormulaSaldoViaje formula = FormulaSaldoViaje.BalanceAnticipo)
    {
        var fleteRedondeado = Redondear(flete);
        var anticipoRedondeado = Redondear(anticipo);
        var saldoAnteriorRedondeado = Redondear(saldoAnterior);

        decimal totalGastos = 0m;
        foreach (var gasto in gastos)
        {
            totalGastos += Redondear(gasto);
        }
        totalGastos = Redondear(totalGastos);

        decimal saldoViaje = formula switch
        {
            FormulaSaldoViaje.BalanceAnticipo => Redondear(totalGastos - anticipoRedondeado),
            FormulaSaldoViaje.LiquidacionFlete => Redondear(fleteRedondeado - anticipoRedondeado - totalGastos),
            _ => Redondear(totalGastos - anticipoRedondeado)
        };

        decimal saldoTotal = Redondear(saldoViaje + saldoAnteriorRedondeado);

        return new ResultadoCalculo(totalGastos, saldoViaje, saldoTotal);
    }

    public static decimal Redondear(decimal valor)
    {
        return decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
