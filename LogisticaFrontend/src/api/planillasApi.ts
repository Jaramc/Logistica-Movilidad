import { apiClient } from './client';
import { 
  PlanillaOcrResponseSchema, 
  type PlanillaOcrResponse,
  type GuardarPlanillaDto 
} from '../features/planillas/schemas/planillaSchema';

/**
 * Servicio de API para el módulo de Planillas y Liquidaciones.
 * Centraliza las rutas hacia el backend con tipado estricto y validación Zod en el borde.
 */

/**
 * 1. Extracción de Planilla con Azure AI Document Intelligence
 * Contrato Objetivo: POST /api/planillas/extraer (campo 'file')
 * TODO(contrato): Ajustar cuando el backend unifique la ruta a /api/planillas/extraer.
 */
export async function extraerPlanillaOcrApi(
  archivo: File,
  signal?: AbortSignal
): Promise<PlanillaOcrResponse> {
  const formData = new FormData();
  formData.append('file', archivo);
  // Compatibilidad temporal con el endpoint legacy de liquidaciones
  formData.append('imagen', archivo);

  let rawResponse: unknown;

  try {
    // 1. Intentar endpoint de la planilla
    rawResponse = await apiClient<unknown>('/api/planilla/procesar-ocr', {
      method: 'POST',
      body: formData,
      signal,
    });
  } catch (error: any) {
    // Si el endpoint devuelve 404, intentar fallback
    if (error.status === 404) {
      rawResponse = await apiClient<unknown>('/api/liquidaciones/extraer-datos', {
        method: 'POST',
        body: formData,
        signal,
      });
    } else {
      throw error;
    }
  }

  // Validación estricta en el borde (Runtime Boundary Validation con Zod)
  const parseResult = PlanillaOcrResponseSchema.safeParse(rawResponse);
  if (!parseResult.success) {
    console.error('Error de contrato al validar respuesta de OCR:', parseResult.error);
    // Si la estructura no encaja exactamente, intentamos un objeto normalizado seguro
    const fallbackData = (rawResponse as any)?.data || (rawResponse as any)?.datos || {};
    return {
      mensaje: (rawResponse as any)?.mensaje ?? 'Datos procesados parcialmente',
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
 * Contrato Objetivo: POST /api/planillas con GuardarPlanillaDto
 * TODO(contrato): Cuando el backend aplique la migración de PlanillaViaje, apuntará directamente a POST /api/planillas.
 */
export async function guardarPlanillaApi(
  dto: GuardarPlanillaDto,
  signal?: AbortSignal
): Promise<{ mensaje: string; id?: number }> {
  // Mapper de compatibilidad para el backend actual (POST /api/liquidaciones/guardar)
  const legacyPayload = {
    ...dto,
    origenViaje: dto.origen,
    jsonOcrCrudo: JSON.stringify(dto),
  };

  try {
    return await apiClient<{ mensaje: string; id?: number }>('/api/liquidaciones/guardar', {
      method: 'POST',
      body: legacyPayload,
      signal,
    });
  } catch (error: any) {
    if (error.status === 404) {
      return await apiClient<{ mensaje: string; id?: number }>('/api/planillas', {
        method: 'POST',
        body: dto,
        signal,
      });
    }
    throw error;
  }
}

/**
 * 3. Descarga de Consolidado Excel
 * Contrato Objetivo: GET /api/planillas/descargar-consolidado
 */
export async function descargarConsolidadoApi(signal?: AbortSignal): Promise<Blob> {
  try {
    return await apiClient<Blob>('/api/liquidaciones/descargar-consolidado', {
      method: 'GET',
      signal,
    });
  } catch (error: any) {
    if (error.status === 404) {
      return await apiClient<Blob>('/api/planillas/descargar-consolidado', {
        method: 'GET',
        signal,
      });
    }
    throw error;
  }
}

/**
 * 4. Obtener Historial de Liquidaciones Registradas
 */
export async function obtenerHistorialApi(signal?: AbortSignal): Promise<unknown[]> {
  return await apiClient<unknown[]>('/api/liquidaciones', {
    method: 'GET',
    signal,
  });
}

