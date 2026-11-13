import { apiClient } from './client';
import { 
  PlanillaOcrResponseSchema, 
  PagedPlanillasSchema,
  type PlanillaOcrResponse,
  type GuardarPlanillaDto,
  type PagedPlanillas
} from '../features/planillas/schemas/planillaSchema';

/**
 * Servicio de API para el módulo de Planillas y Liquidaciones.
 * Utiliza exclusivamente las rutas canónicas del backend (.NET 10 PlanillasController).
 */

/**
 * 1. Extracción de Planilla con Azure AI Document Intelligence
 * Ruta canónica: POST /api/planillas/extraer (multipart/form-data con campo 'file')
 */
export async function extraerPlanillaOcrApi(
  archivo: File,
  signal?: AbortSignal
): Promise<PlanillaOcrResponse> {
  const formData = new FormData();
  formData.append('file', archivo);

  const rawResponse = await apiClient<unknown>('/api/planillas/extraer', {
    method: 'POST',
    body: formData,
    signal,
  });

  // Validación estricta en el borde (Runtime Boundary Validation con Zod)
  const parseResult = PlanillaOcrResponseSchema.safeParse(rawResponse);
  if (!parseResult.success) {
    console.error('Error de contrato al validar respuesta de OCR:', parseResult.error);
    const fallbackData = (rawResponse as any)?.data || (rawResponse as any)?.datos || {};
    return {
      mensaje: (rawResponse as any)?.mensaje ?? 'Datos procesados parcialmente',
      advertencias: (rawResponse as any)?.advertencias ?? [],
      data: {
        placa: String(fallbackData.placa ?? '').toUpperCase(),
        origen: String(fallbackData.origen ?? fallbackData.origenViaje ?? ''),
        destino: String(fallbackData.destino ?? ''),
        flete: Number(fallbackData.flete ?? 0),
        anticipo: Number(fallbackData.anticipo ?? 0),
        totalGastos: Number(fallbackData.totalGastos ?? 0),
        saldoViaje: Number(fallbackData.saldoViaje ?? 0),
        detalleGastos: Array.isArray(fallbackData.detalleGastos)
          ? fallbackData.detalleGastos.map((g: any) => ({
              concepto: String(g.concepto || g.Concepto || 'Gasto'),
              valor: Number(g.valor || g.Valor || 0),
            }))
          : [],
      },
    };
  }

  return parseResult.data;
}

/** Respuesta de guardar: totales calculados por el servidor. */
export interface PlanillaGuardada {
  mensaje: string;
  id: number;
  data: { placa: string; totalGastos: number; saldoViaje: number; saldoTotal: number };
}

/**
 * 2. Guardar Planilla Aprobada en Base de Datos
 * Ruta: POST /api/planillas. No se envían totales: el servidor los calcula.
 */
export async function guardarPlanillaApi(
  dto: GuardarPlanillaDto,
  signal?: AbortSignal
): Promise<PlanillaGuardada> {
  return await apiClient<PlanillaGuardada>('/api/planillas', {
    method: 'POST',
    body: dto,
    signal,
  });
}

export interface RangoFechas {
  /** Formato yyyy-MM-dd */
  desde: string;
  hasta: string;
}

/**
 * 3. Descarga de Consolidado Excel (solo Administrador)
 * Ruta: GET /api/planillas/descargar-consolidado?desde=&hasta= (máximo 1 año)
 */
export async function descargarConsolidadoApi(rango: RangoFechas, signal?: AbortSignal): Promise<Blob> {
  const params = new URLSearchParams({ desde: rango.desde, hasta: rango.hasta });
  return await apiClient<Blob>(`/api/planillas/descargar-consolidado?${params.toString()}`, {
    method: 'GET',
    signal,
  });
}

export interface FiltrosHistorial {
  desde?: string;
  hasta?: string;
  placa?: string;
  conductor?: string;
}

/**
 * 4. Historial paginado
 * Ruta: GET /api/planillas?pagina=&tamano=&desde=&hasta=&placa=&conductor= (tamano máximo 100)
 */
export async function obtenerHistorialApi(
  pagina: number = 1,
  tamano: number = 20,
  filtros: FiltrosHistorial = {},
  signal?: AbortSignal
): Promise<PagedPlanillas> {
  const params = new URLSearchParams({ pagina: String(pagina), tamano: String(tamano) });
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor) params.set(clave, valor);
  }

  const rawResponse = await apiClient<unknown>(`/api/planillas?${params.toString()}`, {
    method: 'GET',
    signal,
  });

  const parseResult = PagedPlanillasSchema.safeParse(rawResponse);
  if (!parseResult.success) {
    throw new Error('Estructura de respuesta no válida al consultar historial de planillas');
  }

  return parseResult.data;
}
