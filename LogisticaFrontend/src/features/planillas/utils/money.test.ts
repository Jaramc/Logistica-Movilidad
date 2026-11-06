import { describe, it, expect } from 'vitest';
import { 
  parseCOP, 
  formatCOP, 
  sumarCOP, 
  restarCOP, 
  toApiDecimalString,
  calcularLiquidacionEconomica 
} from './money';

describe('money.ts - Utilidades Financieras COP y Precisión Contable', () => {
  describe('parseCOP - Desambiguación de Moneda', () => {
    it('interpreta correctamente formato con miles y decimales colombianos: 1.500.000,50', () => {
      const res = parseCOP('1.500.000,50');
      expect(res.toString()).toBe('1500000.5');
      expect(toApiDecimalString(res)).toBe('1500000.50');
    });

    it('interpreta correctamente coma como decimal: 1500,50 y 12,5', () => {
      expect(toApiDecimalString(parseCOP('1500,50'))).toBe('1500.50');
      expect(toApiDecimalString(parseCOP('12,5'))).toBe('12.50');
    });

    it('interpreta correctamente punto como miles sin decimales: 1.500.000', () => {
      expect(toApiDecimalString(parseCOP('1.500.000'))).toBe('1500000.00');
      expect(toApiDecimalString(parseCOP('150.000'))).toBe('150000.00');
    });

    it('CRÍTICO: $ 12.50 se interpreta como 12.50 y NUNCA como 1250', () => {
      const res = parseCOP('$ 12.50');
      expect(res.toString()).toBe('12.5');
      expect(toApiDecimalString(res)).toBe('12.50');
    });

    it('maneja valores con formato anglosajón: 1,500,000.50', () => {
      const res = parseCOP('1,500,000.50');
      expect(toApiDecimalString(res)).toBe('1500000.50');
    });

    it('maneja valores numéricos flotantes nativos', () => {
      expect(toApiDecimalString(parseCOP(6976800))).toBe('6976800.00');
      expect(toApiDecimalString(parseCOP(12.5))).toBe('12.50');
    });

    it('maneja números negativos y símbolos contables', () => {
      expect(toApiDecimalString(parseCOP('-50000'))).toBe('-50000.00');
      expect(toApiDecimalString(parseCOP('-$ 150.000'))).toBe('-150000.00');
      expect(toApiDecimalString(parseCOP('(20.000)'))).toBe('-20000.00');
    });

    it('retorna 0 para valores vacíos, nulos, indefinidos o basura', () => {
      expect(parseCOP('').toString()).toBe('0');
      expect(parseCOP('   ').toString()).toBe('0');
      expect(parseCOP(null).toString()).toBe('0');
      expect(parseCOP(undefined).toString()).toBe('0');
      expect(parseCOP('texto no numérico').toString()).toBe('0');
    });
  });

  describe('formatCOP - Visualización en pesos colombianos', () => {
    it('formatea montos a moneda COP sin decimales por defecto', () => {
      const resultado = formatCOP(2800000);
      // Remueve espacios no separables de Intl si los hubiera
      const limpio = resultado.replace(/\u00a0/g, ' ');
      expect(limpio).toContain('2.800.000');
      expect(limpio).toContain('$');
    });

    it('permite incluir 2 decimales si se solicita', () => {
      const resultado = formatCOP('12.50', true);
      const limpio = resultado.replace(/\u00a0/g, ' ');
      expect(limpio).toContain('12,50');
    });
  });

  describe('sumarCOP y restarCOP - Aritmética sin IEEE 754 float leaks', () => {
    it('suma con precisión exacta centesimal (0.1 + 0.2 = 0.30)', () => {
      const res = sumarCOP(0.1, 0.2);
      expect(res.toString()).toBe('0.3');
      expect(toApiDecimalString(res)).toBe('0.30');
    });

    it('suma múltiples importes de gastos', () => {
      const total = sumarCOP('178000', '32900', '500000', '1500.50');
      expect(toApiDecimalString(total)).toBe('712400.50');
    });

    it('resta valores correctamente', () => {
      const saldo = restarCOP('2869200', '2800000');
      expect(toApiDecimalString(saldo)).toBe('69200.00');
    });
  });

  describe('calcularLiquidacionEconomica - Función pura de balances', () => {
    it('calcula correctamente saldo de viaje y saldo total en modo balance anticipo', () => {
      const gastos = [
        { valor: 178000 },
        { valor: 32900 },
        { valor: 2658300 },
      ]; // Total: 2869200
      const anticipo = 2800000;
      const flete = 6976800;
      const saldoAnterior = 10000;

      const calc = calcularLiquidacionEconomica(flete, anticipo, gastos, saldoAnterior, 'balance_anticipo');

      expect(toApiDecimalString(calc.totalGastos)).toBe('2869200.00');
      // Saldo Viaje = Total Gastos - Anticipo = 2869200 - 2800000 = 69200
      expect(toApiDecimalString(calc.saldoViaje)).toBe('69200.00');
      // Saldo Total = Saldo Viaje + Saldo Anterior = 69200 + 10000 = 79200
      expect(toApiDecimalString(calc.saldoTotal)).toBe('79200.00');
    });

    it('calcula saldo negativo si los gastos fueron menores que el anticipo', () => {
      const gastos = [{ valor: 2000000 }];
      const anticipo = 2500000;
      const calc = calcularLiquidacionEconomica(5000000, anticipo, gastos, 0, 'balance_anticipo');

      // Conductor debe reembolsar 500.000 a la empresa
      expect(toApiDecimalString(calc.saldoViaje)).toBe('-500000.00');
    });
  });
});

