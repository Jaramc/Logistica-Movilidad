import { apiClient } from '../../api/client';

export interface Vehiculo {
  id: number;
  placa: string;
  tipo: string;
  categoriaPeaje: string | null;
  activo: boolean;
}

export const listarVehiculos = (activo?: boolean) => {
  const params = new URLSearchParams();
  if (activo !== undefined) params.set('activo', String(activo));
  const query = params.toString();
  return apiClient<Vehiculo[]>(`/api/vehiculos${query ? `?${query}` : ''}`);
};

export const crearVehiculo = (datos: { placa: string; tipo?: string }) =>
  apiClient<Vehiculo>('/api/vehiculos', { method: 'POST', body: datos });

export const desactivarVehiculo = (id: number) =>
  apiClient<Vehiculo>(`/api/vehiculos/${id}/desactivar`, { method: 'POST' });
