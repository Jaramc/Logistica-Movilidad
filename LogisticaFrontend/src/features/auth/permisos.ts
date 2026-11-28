import type { Rol, UsuarioActual } from './authApi';

// "procesador" ya no es un destino independiente del menú: el procesador de planillas ahora
// exige un viaje (ViajeId) y solo se abre desde el detalle de un viaje en "viajes".
export type Vista = 'viajes' | 'conductores' | 'vehiculos' | 'historial' | 'usuarios' | 'auditoria';

/**
 * Qué ve cada rol en el menú. Es SOLO comodidad de la interfaz:
 * el control real lo hace el backend con sus políticas.
 */
const VISTAS_POR_ROL: Record<Rol, Vista[]> = {
  Consulta: ['viajes', 'conductores', 'vehiculos', 'historial'],
  Revisor: ['viajes', 'conductores', 'vehiculos', 'historial'],
  Administrador: ['viajes', 'conductores', 'vehiculos', 'historial', 'usuarios', 'auditoria'],
};

export function vistasPermitidas(usuario: UsuarioActual | null): Vista[] {
  if (!usuario) return [];
  const vistas = new Set<Vista>();
  for (const rol of usuario.roles) {
    for (const vista of VISTAS_POR_ROL[rol] ?? []) vistas.add(vista);
  }
  const orden: Vista[] = ['viajes', 'conductores', 'vehiculos', 'historial', 'usuarios', 'auditoria'];
  return orden.filter((v) => vistas.has(v));
}

export function puedeDescargarConsolidado(usuario: UsuarioActual | null): boolean {
  return usuario?.roles.includes('Administrador') ?? false;
}

export type PasoAcceso = 'login' | 'configurar2fa' | 'cambiarPassword' | 'aplicacion';

/** Qué pantalla corresponde según el estado de la sesión que devuelve /api/auth/me. */
export function pasoDeAcceso(usuario: UsuarioActual | null): PasoAcceso {
  if (!usuario) return 'login';
  if (!usuario.sesionConMfa) return usuario.tiene2fa ? 'login' : 'configurar2fa';
  if (usuario.debeCambiarPassword) return 'cambiarPassword';
  return 'aplicacion';
}
