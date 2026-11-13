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

/**
 * 2. Guardar Planilla Aprobada en Base de Datos
 * Ruta canónica: POST /api/planillas con GuardarPlanillaDto
 */
export async function guardarPlanillaApi(
  dto: GuardarPlanillaDto,
  signal?: AbortSignal
): Promise<{ mensaje: string; id: number; placa?: string; saldoViaje?: number }> {
  return await apiClient<{ mensaje: string; id: number; placa?: string; saldoViaje?: number }>(
    '/api/planillas',
    {
      method: 'POST',
      body: dto,
      signal,
    }
  );
}

/**
 * 3. Descarga de Consolidado Excel
 * Ruta canónica: GET /api/planillas/descargar-consolidado
 */
export async function descargarConsolidadoApi(signal?: AbortSignal): Promise<Blob> {
  return await apiClient<Blob>('/api/planillas/descargar-consolidado', {
    method: 'GET',
    signal,
  });
}

/**
 * 4. Obtener Historial Paginado de Planillas Registradas
 * Ruta canónica: GET /api/planillas?page={page}&pageSize={pageSize}
 */
export async function obtenerHistorialApi(
  page: number = 1,
  pageSize: number = 20,
  signal?: AbortSignal
): Promise<PagedPlanillas> {
  const rawResponse = await apiClient<unknown>(
    `/api/planillas?page=${page}&pageSize=${pageSize}`,
    {
      method: 'GET',
      signal,
    }
  );

  const payload = (rawResponse as any)?.data ?? rawResponse;
  const parseResult = PagedPlanillasSchema.safeParse(payload);
  if (!parseResult.success) {
    // Tolerancia ante arrays planos legacy o variaciones
    if (Array.isArray(payload)) {
      return {
        items: payload.map((item: any) => ({
          id: Number(item.id ?? 0),
          numeroPlanilla: item.numeroPlanilla ?? null,
          placa: String(item.placa ?? 'N/A'),
          conductor: item.conductor ?? null,
          origen: String(item.origen ?? item.origenViaje ?? 'N/A'),
          destino: String(item.destino ?? 'N/A'),
          fechaViaje: item.fechaViaje ?? null,
          totalGastos: Number(item.totalGastos ?? 0),
          saldoViaje: Number(item.saldoViaje ?? 0),
          estado: String(item.estado ?? item.estadoRevision ?? 'Aprobado'),
        })),
        totalItems: payload.length,
        totalCount: payload.length,
        page: 1,
        pageSize: payload.length || 20,
        totalPages: 1,
      };
    }
    throw new Error('Estructura de respuesta no válida al consultar historial de planillas');
  }

  return parseResult.data;
}
