import { useMutation } from '@tanstack/react-query';
import { descargarConsolidadoApi } from '../../../api/planillasApi';
import { ApiError } from '../../../api/types';

export function useDescargarConsolidado() {
  const mutation = useMutation<Blob, ApiError, void>({
    mutationFn: async () => {
      return await descargarConsolidadoApi();
    },
    retry: 1, // La descarga GET es idempotente, se permite un reintento
    onSuccess: (blob) => {
      // Creación y limpieza segura de object URL en el DOM
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      const fecha = new Date().toISOString().split('T')[0];
      link.setAttribute('download', `Liquidaciones_Consolidadas_${fecha}.xlsx`);
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

