import { useMutation } from '@tanstack/react-query';
import { descargarConsolidadoApi, type RangoFechas } from '../../../api/planillasApi';
import { ApiError } from '../../../api/types';

export function useDescargarConsolidado() {
  const mutation = useMutation<Blob, ApiError, RangoFechas>({
    mutationFn: async (rango: RangoFechas) => {
      return await descargarConsolidadoApi(rango);
    },
    retry: 1, // La descarga GET es idempotente, se permite un reintento
    onSuccess: (blob, rango) => {
      // Creación y limpieza segura de object URL en el DOM
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', `Liquidaciones_Consolidadas_${rango.desde}_${rango.hasta}.xlsx`);
      document.body.appendChild(link);
      link.click();
      link.parentNode?.removeChild(link);
      setTimeout(() => window.URL.revokeObjectURL(url), 1000);
    },
  });

  return {
    descargar: mutation.mutateAsync,
    estaDescargando: mutation.isPending,
    error: mutation.error,
  };
}

