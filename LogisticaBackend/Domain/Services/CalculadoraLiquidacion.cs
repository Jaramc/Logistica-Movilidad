using System.Globalization;
using System.Text.RegularExpressions;
using LogisticaBackend.DTOs;

namespace LogisticaBackend.Domain.Services;

public enum FormulaSaldoViaje
{
    /// <summary>
    /// Fórmula por defecto (Balance de Anticipo del Conductor):
    /// Saldo Viaje = Total Gastos - Anticipo.
    /// Si gastó más del anticipo entregado, el saldo es positivo (la empresa debe reembolsarle dinero).
    /// Si gastó menos, es negativo (el conductor debe reintegrar sobrante).
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

    /// <summary>
    /// Regla de Desambiguación de Moneda Colombiana (COP):
    /// 1. Ambos presentes ('.' y ','):
    ///    - Si '.' antecede a ',' ("1.500.000,50"): '.' son miles, ',' es decimal.
    ///    - Si ',' antecede a '.' ("1,500,000.50"): ',' son miles, '.' es decimal.
    /// 2. Solo coma ',':
    ///    - Si tiene 1 o 2 dígitos al final ("12,50", "1500,5"): ',' es decimal.
    ///    - Si tiene bloques de 3 dígitos ("1,500,000"): ',' son miles.
    /// 3. Solo punto '.':
    ///    - Si tiene 1 o 2 dígitos al final ("12.50", "1500.5"): '.' es decimal (¡NUNCA 1250!).
    ///    - Si tiene bloques de 3 dígitos ("1.500.000", "150.000"): '.' son miles.
    /// </summary>
    public static decimal ParsearMonedaColombiana(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0m;

        var str = texto.Trim();
        bool esNegativo = str.Contains('-') || str.StartsWith('(');

        // Dejar solo dígitos, comas y puntos
        var limpio = Regex.Replace(str, @"[^\d.,]", "");
        if (string.IsNullOrWhiteSpace(limpio))
            return 0m;

        bool tienePunto = limpio.Contains('.');
        bool tieneComa = limpio.Contains(',');
        string resultadoStr;

        if (tienePunto && tieneComa)
        {
            int ultimoPunto = limpio.LastIndexOf('.');
            int ultimaComa = limpio.LastIndexOf(',');

            if (ultimoPunto < ultimaComa)
            {
                // 1.500.000,50 -> 1500000.50
                resultadoStr = limpio.Replace(".", "").Replace(',', '.');
            }
            else
            {
                // 1,500,000.50 -> 1500000.50
                resultadoStr = limpio.Replace(",", "");
            }
        }
        else if (tieneComa)
        {
            var partes = limpio.Split(',');
            string parteDecimal = partes[^1];

            if (partes.Length == 2 && (parteDecimal.Length == 1 || parteDecimal.Length == 2))
            {
                resultadoStr = $"{partes[0]}.{parteDecimal}";
            }
            else
            {
                // Miles: 1,500,000
                resultadoStr = limpio.Replace(",", "");
            }
        }
        else if (tienePunto)
        {
            var partes = limpio.Split('.');
            string parteDecimal = partes[^1];

            if (partes.Length == 2 && (parteDecimal.Length == 1 || parteDecimal.Length == 2))
            {
                // Decimal: 12.50 o 1500.5
                resultadoStr = $"{partes[0]}.{parteDecimal}";
            }
            else
            {
                // Miles: 1.500.000 o 150.000
                resultadoStr = limpio.Replace(".", "");
            }
        }
        else
        {
            resultadoStr = limpio;
        }

        if (decimal.TryParse(resultadoStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
        {
            var final = Redondear(resultado);
            return esNegativo ? -final : final;
        }

        return 0m;
    }
}

