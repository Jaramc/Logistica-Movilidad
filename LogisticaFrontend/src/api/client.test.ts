import { describe, it, expect, afterEach, vi } from 'vitest';
import { apiClient } from './client';
import { ApiError } from './types';

describe('client.ts - Cliente HTTP Unificado y Manejo RFC 7807', () => {
  const originalFetch = globalThis.fetch;

  afterEach(() => {
    globalThis.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('procesa respuestas exitosas en JSON', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => ({ resultado: 'ok' }),
    });

    const res = await apiClient<{ resultado: string }>('/api/test');
    expect(res.resultado).toBe('ok');
  });

  it('captura error 413 (Payload Too Large) y devuelve mensaje de usuario claro', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 413,
      statusText: 'Payload Too Large',
      headers: new Headers({ 'content-type': 'application/problem+json' }),
      json: async () => ({
        status: 413,
        title: 'Payload Too Large',
        detail: 'Request entity too large',
      }),
    });

    await expect(apiClient('/api/upload')).rejects.toThrowError(ApiError);

    try {
      await apiClient('/api/upload');
    } catch (err: any) {
      expect(err).toBeInstanceOf(ApiError);
      expect(err.status).toBe(413);
      expect(err.getMensajeUsuario()).toContain('10 MB');
    }
  });

  it('captura error 415 (Unsupported Media Type)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 415,
      statusText: 'Unsupported Media Type',
      headers: new Headers({ 'content-type': 'application/problem+json' }),
      json: async () => ({
        status: 415,
        title: 'Unsupported Media Type',
      }),
    });

    try {
      await apiClient('/api/upload');
    } catch (err: any) {
      expect(err.status).toBe(415);
      expect(err.getMensajeUsuario()).toContain('PDF, JPG o PNG legítimo');
    }
  });

  it('captura error 429 (Rate Limit) respetando cabecera Retry-After', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 429,
      statusText: 'Too Many Requests',
      headers: new Headers({
        'content-type': 'application/problem+json',
        'Retry-After': '45',
      }),
      json: async () => ({
        status: 429,
        title: 'Too Many Requests',
      }),
    });

    try {
      await apiClient('/api/ocr');
    } catch (err: any) {
      expect(err.status).toBe(429);
      expect(err.retryAfterSeconds).toBe(45);
      expect(err.getMensajeUsuario()).toContain('espera 45 segundos');
    }
  });

  it('captura error 503 (Base de datos desconectada) sin romper el cliente', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      statusText: 'Service Unavailable',
      headers: new Headers({ 'content-type': 'application/problem+json' }),
      json: async () => ({
        status: 503,
        title: 'Servicio no disponible',
        detail: 'La base de datos PostgreSQL no está conectada todavía.',
      }),
    });

    try {
      await apiClient('/api/guardar');
    } catch (err: any) {
      expect(err.status).toBe(503);
      expect(err.getMensajeUsuario()).toContain('PostgreSQL');
    }
  });

  it('captura error 500 y adjunta el correlationId sin filtrar stack traces técnicos', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      headers: new Headers({ 'content-type': 'application/problem+json' }),
      json: async () => ({
        status: 500,
        title: 'Internal Server Error',
        detail: 'System.NullReferenceException at Service.cs:line 182...',
        correlationId: 'REQ-AZURE-998811',
      }),
    });

    try {
      await apiClient('/api/ocr');
    } catch (err: any) {
      expect(err.status).toBe(500);
      expect(err.correlationId).toBe('REQ-AZURE-998811');
      const msg = err.getMensajeUsuario();
      expect(msg).not.toContain('NullReferenceException');
      expect(msg).toContain('REQ-AZURE-998811');
    }
  });

  it('maneja cancelación explícita mediante AbortController', async () => {
    const controller = new AbortController();
    controller.abort('Cancelado por el usuario');

    globalThis.fetch = vi.fn().mockRejectedValue(new DOMException('The user aborted a request.', 'AbortError'));

    await expect(apiClient('/api/test', { signal: controller.signal })).rejects.toThrow(ApiError);
  });
});
