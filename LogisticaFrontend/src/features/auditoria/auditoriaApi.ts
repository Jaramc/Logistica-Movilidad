import { apiClient } from '../../api/client';

export interface RegistroAuditoria {
  id: number;
  fechaUtc: string;
  usuarioId: string | null;
  usuarioEmail: string | null;
  accion: string;
  entidad: string | null;
  entidadId: string | null;
  ip: string | null;
  detalle: string | null;
  exito: boolean;
}

export interface Pagina<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const ACCIONES = [
  'LoginExitoso', 'LoginFallido', 'CuentaBloqueada', 'SegundoFactorExitoso', 'SegundoFactorFallido', 'Logout',
  'CambioPassword', 'DosFactoresConfigurado', 'DosFactoresReiniciado', 'CodigosRecuperacionRegenerados',
  'UsuarioCreado', 'PasswordTemporalRegenerada', 'RolCambiado', 'UsuarioDesactivado', 'UsuarioActivado', 'PlanillaGuardada', 'ConsolidadoDescargado',
];

export function obtenerAuditoria(pagina: number, accion: string): Promise<Pagina<RegistroAuditoria>> {
  const params = new URLSearchParams({ pagina: String(pagina), tamano: '50' });
  if (accion) params.set('accion', accion);
  return apiClient<Pagina<RegistroAuditoria>>(`/api/auditoria?${params.toString()}`);
}
