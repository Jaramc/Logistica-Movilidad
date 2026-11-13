import { createContext, useContext } from 'react';
import type { UsuarioActual } from './authApi';

export const CLAVE_SESION = ['sesion', 'me'] as const;
export const MENSAJE_SESION_EXPIRADA = 'Tu sesión expiró. Inicia sesión de nuevo.';

export interface EstadoSesion {
  /** null = sin sesión. El estado viene SIEMPRE de GET /api/auth/me; nada se guarda en el navegador. */
  usuario: UsuarioActual | null;
  cargando: boolean;
  /** Mensaje para la pantalla de login (p. ej. "Tu sesión expiró"). */
  mensaje: string | null;
  refrescar: () => Promise<void>;
  cerrarSesion: () => Promise<void>;
}

export const SesionContext = createContext<EstadoSesion | null>(null);

export function useSesion(): EstadoSesion {
  const ctx = useContext(SesionContext);
  if (!ctx) throw new Error('useSesion debe usarse dentro de SesionProvider');
  return ctx;
}
