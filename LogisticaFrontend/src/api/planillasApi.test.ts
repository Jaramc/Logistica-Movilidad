import { describe, it, expect, afterEach, vi } from 'vitest';
import { 
  extraerPlanillaOcrApi, 
  guardarPlanillaApi, 
  descargarConsolidadoApi, 
  obtenerHistorialApi 
} from './planillasApi';
import type { GuardarPlanillaDto } from '../features/planillas/schemas/planillaSchema';

describe('planillasApi.ts - Rutas Canónicas del Backend', () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  describe('extraerPlanillaOcrApi', () => {
    it('llama a POST /api/planillas/extraer enviando solo el campo "file"', async () => {
      let capturedUrl = '';
      let capturedBody: FormData | null = null;

      globalThis.fetch = vi.fn().mockImplementation(async (url: string, init: any) => {
        capturedUrl = url;
        capturedBody = init.body;
        return {
          ok: true,
          status: 200,
          headers: new Headers({ 'content-type': 'application/json' }),
          json: async () => ({
            mensaje: 'Planilla procesada',
            advertencias: [],
            data: {
              placa: 'WCN410',
              origen: 'BOGOTA',
              destino: 'MEDELLIN',
              flete: 4500000,
              anticipo: 2000000,
              totalGastos: 1500000,
              saldoViaje: -500000,
              detalleGastos: [
                { concepto: 'PEAJES', valor: 250000 }
              ]
            }
          }),
        };
      });

      const fakeFile = new File(['%PDF-1.4'], 'planilla.pdf', { type: 'application/pdf' });
      const resultado = await extraerPlanillaOcrApi(fakeFile);

      expect(capturedUrl).toContain('/api/planillas/extraer');
      expect(capturedBody).toBeInstanceOf(FormData);
      const fd = capturedBody as unknown as FormData;
      expect(fd.has('file')).toBe(true);
      expect(fd.has('imagen')).toBe(false); // Eliminado campo legacy redundante
      expect(resultado.data?.placa).toBe('WCN410');
      expect(resultado.data?.flete).toBe(4500000);
      expect(resultado.data?.detalleGastos).toHaveLength(1);
    });
  });

  describe('guardarPlanillaApi', () => {
    it('llama a POST /api/planillas con el DTO canónico', async () => {
      let capturedUrl = '';
      let capturedMethod = '';
      let capturedBody = '';

      globalThis.fetch = vi.fn().mockImplementation(async (url: string, init: any) => {
        capturedUrl = url;
        capturedMethod = init.method;
        capturedBody = init.body;
        return {
          ok: true,
          status: 201,
          headers: new Headers({ 'content-type': 'application/json' }),
          json: async () => ({
            mensaje: 'Planilla guardada exitosamente.',
            id: 42,
            placa: 'TLX890',
            saldoViaje: 69200
          }),
        };
      });

      const dto: GuardarPlanillaDto = {
        placa: 'TLX890',
        conductor: 'CARLOS PEREZ',
        origen: 'BUENAVENTURA',
        destino: 'MEDELLIN',
        fechaInicial: '2026-11-01',
        fechaFinal: '2026-11-05',
        flete: 6976800,
        anticipo: 2800000,
        gastos: [
          { concepto: 'PARQUEO', valor: 178000 }
        ]
      };

      const res = await guardarPlanillaApi(dto);
      expect(capturedUrl).toContain('/api/planillas');
      expect(capturedMethod).toBe('POST');
      expect(JSON.parse(capturedBody).placa).toBe('TLX890');
      // Los totales nunca se envían: el servidor los calcula.
      const enviado = JSON.parse(capturedBody);
      expect(enviado).not.toHaveProperty('totalGastos');
      expect(enviado).not.toHaveProperty('saldoViaje');
      expect(enviado).not.toHaveProperty('saldoTotal');
      expect(enviado).not.toHaveProperty('id');
      expect(res.id).toBe(42);
    });
  });

  describe('descargarConsolidadoApi', () => {
    it('llama a GET /api/planillas/descargar-consolidado', async () => {
      let capturedUrl = '';

      globalThis.fetch = vi.fn().mockImplementation(async (url: string) => {
        capturedUrl = url;
        return {
          ok: true,
          status: 200,
          headers: new Headers({
            'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'content-disposition': 'attachment; filename="Consolidado_Planillas.xlsx"'
          }),
          blob: async () => new Blob(['fake-excel-content'], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
          }),
        };
      });

      const blob = await descargarConsolidadoApi({ desde: '2026-01-01', hasta: '2026-06-30' });
      expect(capturedUrl).toBe('/api/planillas/descargar-consolidado?desde=2026-01-01&hasta=2026-06-30');
      expect(blob.size).toBeGreaterThan(0);
    });
  });

  describe('obtenerHistorialApi', () => {
    it('llama a GET /api/planillas?pagina=1&tamano=20 con filtros y parsea PagedPlanillas', async () => {
      let capturedUrl = '';

      globalThis.fetch = vi.fn().mockImplementation(async (url: string) => {
        capturedUrl = url;
        return {
          ok: true,
          status: 200,
          headers: new Headers({ 'content-type': 'application/json' }),
          json: async () => ({
              items: [
                {
                  id: 1,
                  numeroPlanilla: 'PL-100',
                  placa: 'WCN410',
                  conductor: 'JUAN VALDEZ',
                  origen: 'BOGOTA',
                  destino: 'MEDELLIN',
                  fechaViaje: '2026-11-01',
                  totalGastos: 2869200,
                  saldoViaje: 69200,
                  estado: 'Aprobado',
                  createdAt: '2026-11-06T10:00:00Z'
                }
              ],
              totalCount: 1,
              page: 1,
              pageSize: 20,
              totalPages: 1
          }),
        };
      });

      const res = await obtenerHistorialApi(1, 20, { placa: 'WCN410', conductor: '' });
      expect(capturedUrl).toBe('/api/planillas?pagina=1&tamano=20&placa=WCN410');
      expect(res.items).toHaveLength(1);
      expect(res.items[0]!.placa).toBe('WCN410');
      expect(res.totalItems).toBe(1);
      expect(res.totalPages).toBe(1);
    });
  });
});
