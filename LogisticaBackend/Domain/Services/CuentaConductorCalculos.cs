using LogisticaBackend.Domain.Entities;

namespace LogisticaBackend.Domain.Services;

/// <summary>
/// Aritmética pura de la cuenta corriente del conductor. Convención de signo: positivo = la
/// empresa le debe al conductor; negativo = el conductor le debe a la empresa.
/// </summary>
public static class CuentaConductorCalculos
{
    /// <summary>
    /// PagoEmpresa resta (la empresa deja de deberle al conductor); DescuentoNomina suma (el
    /// conductor deja de deberle a la empresa). El valor de entrada siempre es positivo (PagoSaldo).
    /// </summary>
    public static decimal ValorConSigno(TipoPago tipo, decimal valor) =>
        tipo == TipoPago.PagoEmpresa ? -CalculadoraLiquidacion.Redondear(valor) : CalculadoraLiquidacion.Redondear(valor);

    public static decimal SaldoResultante(decimal saldoAnterior, decimal valor) =>
        CalculadoraLiquidacion.Redondear(saldoAnterior + valor);

    /// <summary>Saldo del viaje = Σ gastos aceptados − anticipo (suma de traslados).</summary>
    public static decimal SaldoViaje(decimal totalGastos, decimal anticipoTraslados) =>
        CalculadoraLiquidacion.Redondear(totalGastos - anticipoTraslados);
}
