import { apiClient } from '../../api/client';

export interface PartidaPendiente {
  movimientoOrigenId: number;
  viajeId: number | null;
  /** Con signo: positivo = la empresa le debe al conductor. */
  pendiente: number;
  creadoEn: string;
  antiguedadDias: number;
}

export interface Conductor {
  id: number;
  codigo: string;
  nombreCompleto: string;
  /** Enmascarada ("••••3223") salvo para el rol Administrador. */
  cedula: string;
  activo: boolean;
  creadoEn: string;
  /** Con signo: positivo = la empresa le debe al conductor. */
  saldo: number;
  tieneMovimientos: boolean;
  partidasPendientes: PartidaPendiente[];
}

export interface MovimientoSaldo {
  id: number;
  tipo: 'Inicial' | 'CierreViaje' | 'PagoEmpresa' | 'DescuentoNomina' | 'Ajuste';
  valor: number;
  saldoResultante: number;
  fechaCorte: string;
  justificacion: string | null;
  viajeId: number | null;
  pagoSaldoId: number | null;
  creadoEn: string;
}

export interface Pagina<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const listarConductores = (activo?: boolean) => {
  const params = new URLSearchParams();
  if (activo !== undefined) params.set('activo', String(activo));
  const query = params.toString();
  return apiClient<Conductor[]>(`/api/conductores${query ? `?${query}` : ''}`);
};

export const obtenerConductor = (id: number) => apiClient<Conductor>(`/api/conductores/${id}`);

export const crearConductor = (datos: { nombreCompleto: string; cedula: string }) =>
  apiClient<Conductor>('/api/conductores', { method: 'POST', body: datos });

export const actualizarConductor = (id: number, datos: { nombreCompleto: string }) =>
  apiClient<Conductor>(`/api/conductores/${id}`, { method: 'PUT', body: datos });

export const desactivarConductor = (id: number) =>
  apiClient<Conductor>(`/api/conductores/${id}/desactivar`, { method: 'POST' });

export const registrarSaldoInicial = (id: number, datos: { valor: number; fechaCorte: string; justificacion: string }) =>
  apiClient<MovimientoSaldo>(`/api/conductores/${id}/saldo-inicial`, { method: 'POST', body: datos });

export const obtenerMovimientos = (id: number, pagina = 1, tamano = 20) =>
  apiClient<Pagina<MovimientoSaldo>>(`/api/conductores/${id}/movimientos?pagina=${pagina}&tamano=${tamano}`);

export const registrarPago = (
  id: number,
  datos: { viajeId?: number | null; tipo: 'DescuentoNomina' | 'PagoEmpresa'; valor: number; fecha: string; quincena?: string | null; medio?: string | null; nota?: string | null }
) => apiClient<MovimientoSaldo>(`/api/conductores/${id}/pagos`, { method: 'POST', body: datos });

export const registrarAjuste = (id: number, datos: { valor: number; justificacion: string }) =>
  apiClient<MovimientoSaldo>(`/api/conductores/${id}/ajustes`, { method: 'POST', body: datos });
