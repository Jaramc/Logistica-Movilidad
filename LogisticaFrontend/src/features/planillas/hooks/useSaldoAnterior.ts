import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { obtenerSaldoAnteriorApi } from '../../../api/planillasApi';

/** Normaliza como el backend: espacios simples y mayúsculas. */
const normalizar = (conductor: string) => conductor.trim().replace(/\s+/g, ' ').toUpperCase();

/**
 * Saldo anterior del conductor según el servidor (SaldoTotal de su última planilla).
 * Espera a que se deje de escribir (400 ms) para no consultar con cada tecla.
 */
export function useSaldoAnterior(conductor: string | undefined) {
  const [conductorEstable, setConductorEstable] = useState('');

  useEffect(() => {
    const t = setTimeout(() => setConductorEstable(normalizar(conductor ?? '')), 400);
    return () => clearTimeout(t);
  }, [conductor]);

  const consulta = useQuery({
    queryKey: ['saldo-anterior', conductorEstable],
    queryFn: ({ signal }) => obtenerSaldoAnteriorApi(conductorEstable, signal),
    enabled: conductorEstable.length >= 2,
    staleTime: 30_000,
    retry: false,
  });

  return {
    saldoAnterior: consulta.data?.saldoAnterior ?? null,
    planillaAnteriorId: consulta.data?.planillaAnteriorId ?? null,
    cargando: consulta.isFetching,
  };
}
