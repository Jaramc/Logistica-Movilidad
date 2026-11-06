import { describe, it, expect } from 'vitest';
import { 
  PlanillaOcrResponseSchema, 
  PlanillaFormSchema, 
  GuardarPlanillaDtoSchema 
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
          { id: '1', concepto: 'Peajes', valor: 250000 },
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
});

