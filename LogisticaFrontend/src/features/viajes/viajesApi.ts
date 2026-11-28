import { apiClient } from '../../api/client';

export type EstadoViaje = 'Abierto' | 'EnRevision' | 'Aprobado' | 'Saldado';
export type MedioTraslado = 'Transferencia' | 'Efectivo' | 'Giro';

export interface ViajeResumen {
  id: number;
  conductorId: number;
  conductorCodigo: string;
  conductorNombre: string;
  vehiculoId: number;
  placa: string;
  origen: string;
  destino: string;
  cliente: string;
  flete: number;
  fechaInicio: string;
  fechaFin: string | null;
  estado: EstadoViaje;
  saldoViaje: number | null;
  aprobadoEn: string | null;
}

export interface Traslado {
  id: number;
  fecha: string;
  valor: number;
  medio: MedioTraslado;
  nota: string | null;
  creadoEn: string;
}

export interface ViajeDetalle {
  id: number;
  conductorId: number;
  conductorCodigo: string;
  conductorNombre: string;
  vehiculoId: number;
  placa: string;
  origen: string;
  destino: string;
  cliente: string;
  manifiesto: string | null;
  flete: number;
  fechaInicio: string;
  fechaFin: string | null;
  estado: EstadoViaje;
  saldoViaje: number | null;
  /** Pendiente de la partida de este viaje en la cuenta del conductor; null si aún no se aprueba. */
  pendiente: number | null;
  anticipoTraslados: number;
  planillaId: number | null;
  traslados: Traslado[];
}

export interface Pagina<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FiltrosViajes {
  estado?: EstadoViaje | '';
  conductorId?: number;
  placa?: string;
  desde?: string;
  hasta?: string;
}

export const listarViajes = (pagina = 1, tamano = 20, filtros: FiltrosViajes = {}) => {
  const params = new URLSearchParams({ pagina: String(pagina), tamano: String(tamano) });
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor !== undefined && valor !== '') params.set(clave, String(valor));
  }
  return apiClient<Pagina<ViajeResumen>>(`/api/viajes?${params.toString()}`);
};

export const obtenerViaje = (id: number) => apiClient<ViajeDetalle>(`/api/viajes/${id}`);

export const abrirViaje = (datos: {
  conductorId: number;
  vehiculoId: number;
  origen: string;
  destino: string;
  cliente: string;
  manifiesto?: string | null;
  flete: number;
  fechaInicio: string;
}) => apiClient<ViajeDetalle>('/api/viajes', { method: 'POST', body: datos });

export const registrarTraslado = (viajeId: number, datos: { fecha: string; valor: number; medio: MedioTraslado; nota?: string | null }) =>
  apiClient<Traslado>(`/api/viajes/${viajeId}/traslados`, { method: 'POST', body: datos });

export interface AprobarViajeRespuesta {
  viaje: ViajeDetalle;
  advertencias?: string[] | null;
}

export const aprobarViaje = (viajeId: number) =>
  apiClient<AprobarViajeRespuesta>(`/api/viajes/${viajeId}/aprobar`, { method: 'POST' });

export const reabrirViaje = (viajeId: number, motivo: string) =>
  apiClient<ViajeDetalle>(`/api/viajes/${viajeId}/reabrir`, { method: 'POST', body: { motivo } });
