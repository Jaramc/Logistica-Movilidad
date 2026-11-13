import { apiClient } from '../../api/client';
import type { Rol } from '../auth/authApi';

export const ROLES: Rol[] = ['Administrador', 'Revisor', 'Consulta'];

export interface Usuario {
  id: string;
  email: string;
  nombreCompleto: string;
  rol: Rol | null;
  activo: boolean;
  tiene2fa: boolean;
  debeCambiarPassword: boolean;
  bloqueado: boolean;
  creadoEn: string;
  ultimoAcceso: string | null;
}

export interface UsuarioCreado {
  usuario: Usuario;
  /** Se muestra una sola vez. */
  passwordTemporal: string;
}

export const listarUsuarios = () => apiClient<Usuario[]>('/api/usuarios');

export const crearUsuario = (datos: { email: string; nombreCompleto: string; rol: Rol }) =>
  apiClient<UsuarioCreado>('/api/usuarios', { method: 'POST', body: datos });

export const cambiarRol = (id: string, rol: Rol) =>
  apiClient<Usuario>(`/api/usuarios/${encodeURIComponent(id)}/rol`, { method: 'PUT', body: { rol } });

export const cambiarEstado = (id: string, activo: boolean) =>
  apiClient<Usuario>(`/api/usuarios/${encodeURIComponent(id)}/estado`, { method: 'PUT', body: { activo } });

export const reiniciar2fa = (id: string) =>
  apiClient<Usuario>(`/api/usuarios/${encodeURIComponent(id)}/reiniciar-2fa`, { method: 'POST' });
