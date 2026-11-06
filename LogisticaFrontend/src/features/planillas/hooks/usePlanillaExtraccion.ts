import { useRef, useEffect } from 'react';
import { useMutation } from '@tanstack/react-query';
import { extraerPlanillaOcrApi } from '../../../api/planillasApi';
import { ApiError } from '../../../api/types';
import type { PlanillaOcrResponse } from '../schemas/planillaSchema';

export function usePlanillaExtraccion() {
  const abortControllerRef = useRef<AbortController | null>(null);

  // Cancelar peticiones en vuelo al desmontar el hook
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort('Componente desmontado');
      }
    };
  }, []);

  const mutation = useMutation<PlanillaOcrResponse, ApiError, File>({
    mutationFn: async (archivo: File) => {
      // Si había una extracción previa en vuelo, cancelarla
      if (abortControllerRef.current) {
        abortControllerRef.current.abort('Nueva extracción iniciada');
      }

      abortControllerRef.current = new AbortController();

      return await extraerPlanillaOcrApi(archivo, abortControllerRef.current.signal);
    },
    // Jamás reintentar automáticamente OCR (cuesta dinero en Azure)
    retry: false,
  });

  const cancelarExtraccion = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort('Cancelado por el usuario');
      abortControllerRef.current = null;
    }
  };

  return {
    extraer: mutation.mutateAsync,
    estaExtrayendo: mutation.isPending,
    error: mutation.error,
    datosExtraidos: mutation.data,
    reset: mutation.reset,
    cancelarExtraccion,
  };
}

