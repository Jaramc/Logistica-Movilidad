import { QueryClient } from '@tanstack/react-query';
import type { ApiError } from '../api/types';

export function crearQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          const status = (error as ApiError | undefined)?.status ?? 0;
          // No reintentar en errores de cliente 4xx (incluye 401/403)
          if (status >= 400 && status < 500) return false;
          return failureCount < 2;
        },
      },
      mutations: {
        retry: false, // Por seguridad y costo, nunca reintentar mutaciones automáticamente
      },
    },
  });
}
