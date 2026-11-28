namespace LogisticaBackend.Domain.Services;

public enum FormulaSaldoViaje
{
    /// <summary>
    /// Fórmula oficial (Balance de Anticipo del Conductor):
    /// Saldo Viaje = Total Gastos - Anticipo; Saldo Total (saldo nuevo) = Saldo Viaje + Saldo Anterior.
    /// Positivo = la empresa le debe al conductor; negativo = el conductor le debe a la empresa.
    /// Confirmada con cinco planillas reales encadenadas (el saldo nuevo de una es el anterior de la
    /// siguiente), p. ej. la de Cliente Ejemplo (13 al 19-06-26): 2.869.200 - 2.800.000 = 69.200 a favor
    /// del conductor, y 69.200 - 63.500 (a favor de la empresa) = 5.700 a favor del conductor.
    /// Ver docs/ocr/planilla_real_01.campos.json.
    /// </summary>
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
