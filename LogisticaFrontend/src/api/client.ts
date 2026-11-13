import { ApiError, type ProblemDetails } from './types';

/**
 * Por defecto la API se consume en el MISMO origen (rutas relativas /api/...):
 * en desarrollo a través del proxy de Vite y en producción a través de Caddy.
 * Así la cookie de sesión (__Host-, SameSite=Strict) y la cookie XSRF-TOKEN funcionan sin CORS.
 */
export const API_BASE_URL: string = (import.meta.env.VITE_API_URL || '').replace(/\/+$/, '');

export const CSRF_COOKIE = 'XSRF-TOKEN';
export const CSRF_HEADER = 'X-XSRF-TOKEN';
const METODOS_SEGUROS = new Set(['GET', 'HEAD', 'OPTIONS', 'TRACE']);

/** Lee la cookie XSRF-TOKEN que entrega el backend (no es HttpOnly a propósito). */
export function leerTokenCsrf(): string | null {
  const par = document.cookie
    .split(';')
    .map((c) => c.trim())
    .find((c) => c.startsWith(`${CSRF_COOKIE}=`));
  return par ? decodeURIComponent(par.slice(CSRF_COOKIE.length + 1)) : null;
}

/** Pide al backend una cookie XSRF-TOKEN nueva. */
export async function renovarTokenCsrf(): Promise<void> {
  await fetch(`${API_BASE_URL}/api/auth/antiforgery`, { method: 'GET', credentials: 'include' });
}

const DEFAULT_TIMEOUT_MS = 60000; // 60 segundos para operaciones de IA OCR

export interface FetchOptions extends Omit<RequestInit, 'body'> {
  body?: BodyInit | Record<string, unknown> | null;
  timeoutMs?: number;
}

/**
 * Cliente HTTP Unificado para Logística y Movilidad.
 * Centraliza timeout, AbortController, y parseo estricto de ProblemDetails (RFC 7807).
 */
export async function apiClient<T>(endpoint: string, options: FetchOptions = {}): Promise<T> {
  const {
    body,
    headers: headersCustom,
    timeoutMs = DEFAULT_TIMEOUT_MS,
    signal: userSignal,
    ...restOptions
  } = options;

  if (!endpoint.startsWith('/')) {
    // Nunca se envían credenciales a URLs absolutas arbitrarias.
    throw new Error(`Ruta de API inválida: ${endpoint}`);
  }
  const url = `${API_BASE_URL}${endpoint}`;
  const metodo = (restOptions.method ?? 'GET').toUpperCase();

  // Combinación de AbortSignals para soportar cancelación de usuario y timeout
  const timeoutController = new AbortController();
  const timeoutId = setTimeout(() => {
    timeoutController.abort(new Error(`Timeout de red superado (${timeoutMs / 1000}s)`));
  }, timeoutMs);

  let combinedSignal: AbortSignal;
  if (userSignal) {
    if (typeof AbortSignal.any === 'function') {
      combinedSignal = AbortSignal.any([userSignal, timeoutController.signal]);
    } else {
      combinedSignal = timeoutController.signal;
      userSignal.addEventListener('abort', () => timeoutController.abort(userSignal.reason), { once: true });
    }
  } else {
    combinedSignal = timeoutController.signal;
  }

  const headers = new Headers(headersCustom);

  let payload: BodyInit | null = null;
  if (body instanceof FormData || body instanceof Blob || typeof body === 'string') {
    payload = body;
  } else if (body && typeof body === 'object') {
    headers.set('Content-Type', 'application/json');
    payload = JSON.stringify(body);
  }

  headers.set('Accept', 'application/json, application/problem+json');

  try {
    if (!METODOS_SEGUROS.has(metodo)) {
      let token = leerTokenCsrf();
      if (!token) {
        await renovarTokenCsrf();
        token = leerTokenCsrf();
      }
      if (token) headers.set(CSRF_HEADER, token);
    }

    const respuesta = await fetch(url, {
      ...restOptions,
      method: metodo,
      credentials: 'include',
      headers,
      body: payload,
      signal: combinedSignal,
    });

    clearTimeout(timeoutId);

    if (!respuesta.ok) {
      await parsearYRechazarError(respuesta);
    }

    // Si la respuesta es de tipo blob (ej. descarga de Excel)
    const contentType = respuesta.headers.get('content-type') || '';
    if (
      contentType.includes('application/vnd.openxmlformats') ||
      contentType.includes('application/octet-stream')
    ) {
      return (await respuesta.blob()) as unknown as T;
    }

    if (respuesta.status === 204) {
      return {} as T;
    }

    return (await respuesta.json()) as T;
  } catch (error: unknown) {
    clearTimeout(timeoutId);

    if (error instanceof ApiError) {
      throw error;
    }

    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new ApiError({
        status: 499,
        title: 'Operación Cancelada',
        detail: 'La petición fue cancelada o superó el tiempo máximo de espera.',
      });
    }

    // Error de red (servidor apagado, conexión caída)
    const mensajeOffline = !navigator.onLine 
      ? 'No hay conexión a internet. Verifica tu red.' 
      : 'No se pudo conectar con el servidor. Verifica que el backend esté en ejecución.';

    throw new ApiError({
      status: 0,
      title: 'Fallo de Conexión',
      detail: mensajeOffline,
    });
  }
}

/**
 * Parsea respuestas de error HTTP conforme a RFC 7807 (ProblemDetails).
 */
async function parsearYRechazarError(respuesta: Response): Promise<never> {
  const status = respuesta.status;
  const retryAfterHeader = respuesta.headers.get('Retry-After');
  const retryAfterSeconds = retryAfterHeader ? parseInt(retryAfterHeader, 10) || undefined : undefined;

  let title = respuesta.statusText || 'Error en la petición';
  let detail = '';
  let correlationId: string | undefined;
  let fieldErrors: Record<string, string[]> | undefined;

  try {
    const json = (await respuesta.json()) as ProblemDetails;
    if (json) {
      title = json.title || (json as any).mensaje || title;
      detail = json.detail || (json as any).detalle || (json as any).error || '';
      correlationId = json.correlationId || (json as any).traceId || (json as any).id;
      fieldErrors = json.errors;
    }
  } catch {
    // Si la respuesta no era JSON
    detail = await respuesta.text().catch(() => '');
  }

  throw new ApiError({
    status,
    title,
    detail,
    correlationId,
    fieldErrors,
    retryAfterSeconds,
  });
}

