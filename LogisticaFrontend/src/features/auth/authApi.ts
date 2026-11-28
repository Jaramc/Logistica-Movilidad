import { apiClient, renovarTokenCsrf } from '../../api/client';
import { ApiError } from '../../api/types';

export type Rol = 'Administrador' | 'Revisor' | 'Consulta';

export interface UsuarioActual {
  id: string;
  email: string;
  nombreCompleto: string;
  roles: Rol[];
  tiene2fa: boolean;
  sesionConMfa: boolean;
  debeCambiarPassword: boolean;
}

export interface LoginRespuesta {
  requiere2fa: boolean;
}

export interface Configurar2faRespuesta {
  clave: string;
  uriOtpauth: string;
}

export interface CodigosRecuperacion {
  codigos: string[];
}

/** Devuelve el usuario de la sesión, o null si no hay sesión (401). */
export async function obtenerUsuarioActual(): Promise<UsuarioActual | null> {
  try {
    return await apiClient<UsuarioActual>('/api/auth/me');
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export async function iniciarSesion(email: string, password: string): Promise<LoginRespuesta> {
  return apiClient<LoginRespuesta>('/api/auth/login', { method: 'POST', body: { email, password } });
}

export async function verificarCodigo2fa(codigo: string): Promise<UsuarioActual> {
  return apiClient<UsuarioActual>('/api/auth/login-2fa', { method: 'POST', body: { codigo } });
}

export async function usarCodigoRecuperacion(codigo: string): Promise<UsuarioActual> {
  return apiClient<UsuarioActual>('/api/auth/login-recuperacion', { method: 'POST', body: { codigo } });
}

export async function cerrarSesionApi(): Promise<void> {
  try {
    await apiClient<void>('/api/auth/logout', { method: 'POST' });
  } finally {
    // El token antiforgery va ligado al usuario: se pide uno nuevo (anónimo).
    await renovarTokenCsrf().catch(() => undefined);
  }
}

export async function cambiarPassword(passwordActual: string, passwordNueva: string): Promise<void> {
  await apiClient<void>('/api/auth/cambiar-password', { method: 'POST', body: { passwordActual, passwordNueva } });
}

export async function configurar2fa(): Promise<Configurar2faRespuesta> {
  return apiClient<Configurar2faRespuesta>('/api/auth/2fa/configurar', { method: 'POST' });
}

export async function confirmar2fa(codigo: string): Promise<CodigosRecuperacion> {
  return apiClient<CodigosRecuperacion>('/api/auth/2fa/verificar', { method: 'POST', body: { codigo } });
}

export async function regenerarCodigos(): Promise<CodigosRecuperacion> {
  return apiClient<CodigosRecuperacion>('/api/auth/2fa/regenerar-codigos', { method: 'POST' });
}
