import { useMutation } from '@tanstack/react-query';
import { guardarPlanillaApi } from '../../../api/planillasApi';
import { ApiError } from '../../../api/types';
import type { GuardarPlanillaDto } from '../schemas/planillaSchema';

export function useGuardarPlanilla() {
  const mutation = useMutation<{ mensaje: string; id?: number }, ApiError, GuardarPlanillaDto>({
    mutationFn: async (dto: GuardarPlanillaDto) => {
      return await guardarPlanillaApi(dto);
    },
    // Jamás reintentar automáticamente mutaciones de guardado para evitar duplicados en base de datos
    retry: false,
  });

  return {
    guardar: mutation.mutateAsync,
    estaGuardando: mutation.isPending,
    esExitoso: mutation.isSuccess,
    error: mutation.error,
    resultado: mutation.data,
    reset: mutation.reset,
  };
}

