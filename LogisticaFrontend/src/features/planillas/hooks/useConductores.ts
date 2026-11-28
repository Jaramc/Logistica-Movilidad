import { useQuery } from '@tanstack/react-query';
import { obtenerConductoresApi } from '../../../api/planillasApi';

/** Conductores que ya tienen planillas: se sugieren al escribir para no partir la cadena de saldos. */
export function useConductores() {
  const consulta = useQuery({
    queryKey: ['conductores'],
    queryFn: ({ signal }) => obtenerConductoresApi(signal),
    staleTime: 60_000,
    retry: false,
  });
  return consulta.data ?? [];
}
