import { describe, it, expect } from 'vitest';
import {
  PlanillaOcrResponseSchema,
  PlanillaFormSchema,
  GuardarPlanillaDtoSchema,
  esDatoNoGasto,
  conceptoDeGasto,
} from './planillaSchema';

describe('planillaSchema.ts - Contratos Zod y Validación en el Borde', () => {
  describe('PlanillaOcrResponseSchema', () => {
    it('valida exitosamente la respuesta típica del modelo planillas-gastos-v1', () => {
      const apiResponse = {
        mensaje: 'Planilla procesada exitosamente.',
        data: {
          placa: 's14',
          origen: 'Buenaventura',
          destino: 'Medellin',
          flete: 6976800,
          anticipo: 2800000,
          totalGastos: 2869200,
          saldoViaje: 69200,
          detalleGastos: [
            { concepto: 'FLETE', valor: 6976800 },
            { concepto: 'ANTICIPO', valor: 2800000 },
            { concepto: 'PARQUEO', valor: 178000 },
            { concepto: 'Papeleria y pasajes', valor: 32900 },
          ],
        },
      };

      const parsed = PlanillaOcrResponseSchema.safeParse(apiResponse);
      expect(parsed.success).toBe(true);
      if (parsed.success) {
        expect(parsed.data.data?.placa).toBe('S14'); // Normaliza a mayúsculas
        expect(parsed.data.data?.flete).toBe(6976800);
        expect(parsed.data.data?.detalleGastos).toHaveLength(4);
      }
    });

    it('es tolerante ante campos nulos o ausentes de Azure sin romper la aplicación', () => {
      const apiResponseNula = {
        mensaje: 'Procesado con celdas vacías',
        data: {
          placa: null,
          origen: null,
          destino: null,
          flete: null,
          anticipo: null,
          totalGastos: null,
          saldoViaje: null,
          detalleGastos: null,
        },
      };

      const parsed = PlanillaOcrResponseSchema.safeParse(apiResponseNula);
      expect(parsed.success).toBe(true);
      if (parsed.success) {
        expect(parsed.data.data?.placa).toBe('');
        expect(parsed.data.data?.flete).toBe(0);
        expect(parsed.data.data?.detalleGastos).toEqual([]);
      }
    });
  });

  describe('PlanillaFormSchema', () => {
    it('valida datos correctos del formulario', () => {
      const formData = {
        placa: 'WCN452',
        conductor: 'Carlos Mendoza',
        origen: 'Buenaventura',
        destino: 'Medellín',
        fechaInicial: '2026-11-01',
        fechaFinal: '2026-11-03',
        manifiesto: 'MAN-101',
        empresaTransportadora: 'Transportes del Valle',
        flete: 5000000,
        anticipo: 2000000,
        saldoAnterior: 0,
        detalleGastos: [
          { id: '1', etiqueta: 'PEAJES', descripcion: '', categoria: 'PEAJES', valor: 250000 },
        ],
      };

      const parsed = PlanillaFormSchema.safeParse(formData);
      expect(parsed.success).toBe(true);
    });

    it('rechaza placa con menos de 3 caracteres', () => {
      const invalido = {
        placa: 'AB',
        conductor: 'Juan',
        origen: 'Cali',
        destino: 'Bogota',
        fechaInicial: '2026-11-01',
        fechaFinal: '2026-11-02',
        manifiesto: '',
        empresaTransportadora: '',
        flete: 1000,
        anticipo: 500,
        saldoAnterior: 0,
        detalleGastos: [],
      };

      const parsed = PlanillaFormSchema.safeParse(invalido);
      expect(parsed.success).toBe(false);
    });
  });

  describe('GuardarPlanillaDtoSchema', () => {
    it('valida el DTO de guardado para la base de datos', () => {
      const dto = {
        placa: 'WCN452',
        conductor: 'Carlos Mendoza',
        origen: 'Buenaventura',
        destino: 'Medellín',
        fechaInicial: '2026-11-01',
        fechaFinal: '2026-11-03',
        flete: 5000000,
        anticipo: 2000000,
        totalGastos: 1500000,
        saldoViaje: -500000,
        saldoAnterior: 0,
        saldoTotal: -500000,
        gastos: [
          { concepto: 'ACPM', valor: 1200000 },
          { concepto: 'Peajes', valor: 300000 },
        ],
      };

      const parsed = GuardarPlanillaDtoSchema.safeParse(dto);
      expect(parsed.success).toBe(true);
    });
  });

  describe('PlanillaFormSchema — reglas de la planilla real', () => {
    const base = {
      placa: 'STW514', conductor: 'Pedro', origen: 'Buenaventura', destino: 'Medellín',
      fechaInicial: '2026-06-13', fechaFinal: '2026-06-19', manifiesto: '', empresaTransportadora: 'Cliente Ejemplo',
      flete: 6976800, anticipo: 2800000, saldoAnterior: 0,
      detalleGastos: [{ id: '1', etiqueta: 'PEAJES', descripcion: '', categoria: 'PEAJES' as const, valor: 32900 }],
    };
    const errores = (cambios: Record<string, unknown>) => {
      const r = PlanillaFormSchema.safeParse({ ...base, ...cambios });
      return r.success ? [] : r.error.issues.map((i) => `${i.path.join('.')}: ${i.message}`);
    };

    it('acepta la planilla válida y placas con espacio o guion', () => {
      expect(errores({})).toEqual([]);
      expect(errores({ placa: 'stw 514' })).toEqual([]);
      expect(errores({ placa: 'STW-514' })).toEqual([]);
    });

    it.each(['514', 'S14', 'WCN4100', 'ABCDEF'])('rechaza la placa %s', (placa) => {
      expect(errores({ placa })[0]).toMatch(/^placa: La placa debe tener 3 letras y 3 números/);
    });

    it.each(['0', '123', '$ -', 'A'])('rechaza la empresa sin letras: %s', (empresaTransportadora) => {
      expect(errores({ empresaTransportadora })).toEqual(['empresaTransportadora: La empresa debe tener letras']);
    });

    it('acepta la empresa vacía o con dos letras', () => {
      expect(errores({ empresaTransportadora: '' })).toEqual([]);
      expect(errores({ empresaTransportadora: 'JR' })).toEqual([]);
    });

    it('rechaza fechas inexistentes y fecha final anterior a la inicial', () => {
      expect(errores({ fechaInicial: '2026-02-30' })[0]).toMatch(/^fechaInicial:/);
      expect(errores({ fechaInicial: '2026-06-19', fechaFinal: '2026-06-13' }))
        .toEqual(['fechaFinal: La fecha final no puede ser anterior a la inicial']);
    });

    it('un gasto libre necesita descripción y nunca puede ser un dato de cabecera o saldo', () => {
      const gasto = (g: Record<string, unknown>) => ({
        detalleGastos: [{ id: '1', etiqueta: '', descripcion: '', categoria: 'OTROS', valor: 1000, ...g }],
      });
      expect(errores(gasto({}))).toEqual(['detalleGastos.0.descripcion: Escribe una descripción del gasto']);
      expect(errores(gasto({ descripcion: 'Total gastos' })))
        .toEqual(['detalleGastos.0.descripcion: Esto es un dato de cabecera o de saldos, no un gasto']);
      expect(errores(gasto({ descripcion: 'Pago bodegaje contenedor' }))).toEqual([]);
      expect(errores(gasto({ categoria: 'GASOLINA', descripcion: 'x' }))[0]).toMatch(/categoria/);
    });
  });

  describe('esDatoNoGasto y conceptoDeGasto', () => {
    it.each([
      ['TOTAL GASTOS', true], ['Saldo anterior', true], ['N° Manifiesto', true], ['FECHA INICIO', true],
      ['Pago bodegaje', false], ['COMISION', false], ['', false],
    ] as const)('%s → %s', (texto, esperado) => {
      expect(esDatoNoGasto(texto)).toBe(esperado);
    });

    it('el concepto es la etiqueta impresa; si no hay, la descripción', () => {
      expect(conceptoDeGasto({ etiqueta: 'COMISION', descripcion: '2 codos vigias' })).toBe('COMISION');
      expect(conceptoDeGasto({ etiqueta: '', descripcion: 'Pago bodegaje' })).toBe('Pago bodegaje');
      expect(conceptoDeGasto({ etiqueta: '', descripcion: ' ' })).toBe('Gasto sin descripción');
    });
  });
});

