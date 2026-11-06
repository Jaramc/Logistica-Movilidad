import { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Navbar } from './components/Navbar';
import ProcesadorLiquidaciones from './components/ProcesadorLiquidaciones';
import { HistorialLiquidaciones } from './components/HistorialLiquidaciones';
import { ErrorBoundary } from './components/ErrorBoundary';

// Inicialización de QueryClient con configuraciones de producción
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: (failureCount, error: any) => {
        // No reintentar en errores de cliente 4xx
        if (error?.status >= 400 && error?.status < 500) return false;
        return failureCount < 2;
      },
    },
    mutations: {
      retry: false, // Por seguridad y costo, nunca reintentar mutaciones automáticamente
    },
  },
});

function App() {
  const [vistaActiva, setVistaActiva] = useState<'procesador' | 'historial'>('procesador');

  return (
    <QueryClientProvider client={queryClient}>
      <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
        <Navbar vistaActiva={vistaActiva} setVistaActiva={setVistaActiva} />

        <main className="flex-1">
          <ErrorBoundary fallbackTitle="Error en el módulo de liquidaciones">
            {vistaActiva === 'procesador' ? (
              <ProcesadorLiquidaciones />
            ) : (
              <HistorialLiquidaciones />
            )}
          </ErrorBoundary>
        </main>

        <footer className="bg-white border-t border-slate-200 py-4 text-center text-xs text-slate-500">
          Logística & Movilidad • Digitalización de Planillas de Gastos con Azure AI y PostgreSQL
        </footer>
      </div>
    </QueryClientProvider>
  );
}

export default App;
