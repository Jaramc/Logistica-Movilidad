import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { alExpirarSesion, renovarTokenCsrf } from '../../api/client';
import { cerrarSesionApi, obtenerUsuarioActual } from './authApi';
import { CLAVE_SESION, MENSAJE_SESION_EXPIRADA, SesionContext, type EstadoSesion } from './sesionContext';


export function SesionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [mensaje, setMensaje] = useState<string | null>(null);

  const { data, isPending, refetch } = useQuery({
    queryKey: CLAVE_SESION,
    queryFn: obtenerUsuarioActual,
    retry: false,
    staleTime: 60_000,
    refetchOnWindowFocus: true, // detecta sesiones cerradas desde el servidor al volver a la pestaña
  });

  // Token antiforgery anónimo para el primer POST (login).
  useEffect(() => {
    renovarTokenCsrf().catch(() => undefined);
  }, []);

  // Ante un 401 en cualquier ruta protegida: limpiar todo y volver al login con aviso.
  useEffect(
    () =>
      alExpirarSesion(() => {
        queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== CLAVE_SESION[0] });
        queryClient.setQueryData(CLAVE_SESION, null);
        setMensaje(MENSAJE_SESION_EXPIRADA);
      }),
    [queryClient]
  );

  const refrescar = useCallback(async () => {
    setMensaje(null);
    await refetch();
  }, [refetch]);

  const cerrarSesion = useCallback(async () => {
    try {
      await cerrarSesionApi();
    } finally {
      // No se usa clear(): desconectaría el observador de la sesión y la pantalla no cambiaría.
      queryClient.removeQueries({ predicate: (q) => q.queryKey[0] !== CLAVE_SESION[0] });
      queryClient.setQueryData(CLAVE_SESION, null);
      setMensaje(null);
    }
  }, [queryClient]);

  const valor = useMemo<EstadoSesion>(
    () => ({ usuario: data ?? null, cargando: isPending, mensaje, refrescar, cerrarSesion }),
    [data, isPending, mensaje, refrescar, cerrarSesion]
  );

  return <SesionContext.Provider value={valor}>{children}</SesionContext.Provider>;
}
