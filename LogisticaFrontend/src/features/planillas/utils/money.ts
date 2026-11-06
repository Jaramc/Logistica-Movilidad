import Big, { type BigSource } from 'big.js';

// Configuración de Big.js para precisión contable
// Round half-up (ROUND_HALF_UP = 1) para redondeo financiero estándar
Big.RM = 1;
Big.DP = 2;

export interface CalculoFinanciero {
  totalGastos: Big;
  saldoViaje: Big;
  saldoTotal: Big;
}

export interface GastoMontoItem {
  valor: BigSource;
}

/**
 * Regla de Desambiguación de Moneda Colombiana (COP):
 * - Oficialmente en Colombia, el separador de miles es '.' y el de decimales es ','.
 * - Sin embargo, el OCR y teclados numéricos en inglés producen '.' como decimal o ',' como miles.
 * Reglas de desambiguación:
 * 1. Ambos presentes ('.' y ','):
 *    - Si '.' antecede a ',' (ej. "1.500.000,50"): '.' son miles, ',' es decimal.
 *    - Si ',' antecede a '.' (ej. "1,500,000.50"): ',' son miles, '.' es decimal.
 * 2. Solo coma ',':
 *    - Si tiene 1 o 2 dígitos al final (ej. "12,50", "1500,5"): ',' es decimal.
 *    - Si está seguido de bloques de 3 dígitos (ej. "1,500,000"): ',' son miles.
 * 3. Solo punto '.':
 *    - Si tiene 1 o 2 dígitos al final (ej. "12.50", "1500.5"): '.' es decimal (¡NUNCA "1250"!).
 *    - Si tiene exactamente 3 dígitos al final o múltiples bloques de 3 (ej. "1.500.000", "150.000"): '.' son miles.
 * 4. Basura o vacío: Retorna Big(0).
 */
export function parseCOP(entrada: unknown): Big {
  if (entrada === null || entrada === undefined) {
    return new Big(0);
  }

  if (typeof entrada === 'number') {
    if (isNaN(entrada) || !isFinite(entrada)) return new Big(0);
    return new Big(entrada.toFixed(2));
  }

  if (entrada instanceof Big) {
    return entrada;
  }

  const str = String(entrada).trim();
  if (!str) return new Big(0);

  // Detectar signo negativo
  const esNegativo = str.includes('-') || str.startsWith('(');

  // Extraer únicamente caracteres numéricos, comas y puntos
  let limpio = str.replace(/[^\d.,]/g, '');
  if (!limpio) return new Big(0);

  const tieneComa = limpio.includes(',');
  const tienePunto = limpio.includes('.');

  let resultadoStr = '0';

  if (tienePunto && tieneComa) {
    const ultimoPunto = limpio.lastIndexOf('.');
    const ultimaComa = limpio.lastIndexOf(',');

    if (ultimoPunto < ultimaComa) {
      // Estilo latinoamericano / colombiano: 1.500.000,50
      limpio = limpio.replace(/\./g, '').replace(',', '.');
    } else {
      // Estilo anglosajón: 1,500,000.50
      limpio = limpio.replace(/,/g, '');
    }
    resultadoStr = limpio;
  } else if (tieneComa) {
    const partes = limpio.split(',');
    const parteDecimal = partes[partes.length - 1] ?? '';

    // Si la última parte tiene 1 o 2 dígitos, es decimal
    if (partes.length === 2 && (parteDecimal.length === 1 || parteDecimal.length === 2)) {
      resultadoStr = `${partes[0] ?? '0'}.${parteDecimal}`;
    } else {
      // Es separador de miles: 1,500,000
      resultadoStr = limpio.replace(/,/g, '');
    }
  } else if (tienePunto) {
    const partes = limpio.split('.');
    const parteDecimal = partes[partes.length - 1] ?? '';

    // Si tiene un solo punto y 1 o 2 dígitos finales (ej. 12.50, 450.5), es decimal
    if (partes.length === 2 && (parteDecimal.length === 1 || parteDecimal.length === 2)) {
      resultadoStr = `${partes[0] ?? '0'}.${parteDecimal}`;
    } else {
      // Separador de miles: 1.500.000 o 150.000
      resultadoStr = limpio.replace(/\./g, '');
    }
  } else {
    // Solo dígitos enteros
    resultadoStr = limpio;
  }

  try {
    const b = new Big(resultadoStr);
    return esNegativo ? b.times(-1) : b;
  } catch {
    return new Big(0);
  }
}

/**
 * Formateo estricto para moneda colombiana (COP) sin pérdida de precisión.
 */
export function formatCOP(valor: BigSource, incluirDecimales = false): string {
  try {
    const b = parseCOP(valor);
    const num = Number(b.toFixed(incluirDecimales ? 2 : 0));

    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: incluirDecimales ? 2 : 0,
      maximumFractionDigits: incluirDecimales ? 2 : 0,
    }).format(num);
  } catch {
    return '$ 0';
  }
}

/**
 * Suma exacta de una lista de importes mediante Big.js.
 */
export function sumarCOP(...valores: BigSource[]): Big {
  return valores.reduce<Big>((acum, val) => {
    return acum.plus(parseCOP(val));
  }, new Big(0));
}

/**
 * Resta exacta: a - b mediante Big.js.
 */
export function restarCOP(a: BigSource, b: BigSource): Big {
  return parseCOP(a).minus(parseCOP(b));
}

/**
 * Convierte un importe a string decimal con exactamente 2 decimales para la API ("1250000.00").
 */
export function toApiDecimalString(valor: BigSource): string {
  return parseCOP(valor).toFixed(2);
}

/**
 * Convierte un importe a número con 2 decimales seguros para DTOs.
 */
export function toApiDecimalNumber(valor: BigSource): number {
  return Number(parseCOP(valor).toFixed(2));
}

export type ModoCalculoSaldo = 'balance_anticipo' | 'liquidacion_flete';

/**
 * Cálculo Puro de Totales y Saldos de la Planilla:
 * 
 * NOTA DE NEGOCIO SOBRE AMBIGÜEDAD DE FÓRMULA:
 * - Modo 'balance_anticipo' (Por defecto en el código existente):
 *   Saldo Viaje = Total Gastos - Anticipo.
 *   Mide la cuenta corriente del conductor: si gastó más que el anticipo entregado,
 *   el saldo es positivo (la empresa debe reembolsarle dinero). Si gastó menos,
 *   es negativo (el conductor debe reintegrar sobrante).
 * - Modo 'liquidacion_flete' (Fórmula de utilidad de viaje transportador):
 *   Saldo Viaje = Flete - Anticipo - Total Gastos.
 */
export function calcularLiquidacionEconomica(
  flete: BigSource,
  anticipo: BigSource,
  gastos: GastoMontoItem[],
  saldoAnterior: BigSource = 0,
  modo: ModoCalculoSaldo = 'balance_anticipo'
): CalculoFinanciero {
  const fleteBig = parseCOP(flete);
  const anticipoBig = parseCOP(anticipo);
  const saldoAnteriorBig = parseCOP(saldoAnterior);

  // Suma exacta de todos los rubros de gastos
  const totalGastosBig = gastos.reduce<Big>((acum, item) => {
    return acum.plus(parseCOP(item.valor));
  }, new Big(0));

  let saldoViajeBig: Big;
  if (modo === 'balance_anticipo') {
    // Total Gastos - Anticipo
    saldoViajeBig = totalGastosBig.minus(anticipoBig);
  } else {
    // Flete - Anticipo - Total Gastos
    saldoViajeBig = fleteBig.minus(anticipoBig).minus(totalGastosBig);
  }

  // Saldo Total = Saldo Viaje + Saldo Anterior
  const saldoTotalBig = saldoViajeBig.plus(saldoAnteriorBig);

  return {
    totalGastos: totalGastosBig,
    saldoViaje: saldoViajeBig,
    saldoTotal: saldoTotalBig,
  };
}

