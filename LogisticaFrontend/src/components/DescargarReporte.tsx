import { Download } from 'lucide-react';
import { useDescargarConsolidado } from '../features/planillas/hooks/useDescargarConsolidado';
import { Button } from './ui/Button';

export default function DescargarReporte() {
  const { descargar, estaDescargando, error } = useDescargarConsolidado();

  const handleDescargar = async () => {
    try {
      await descargar();
    } catch {
      // Error expuesto por useDescargarConsolidado
    }
  };

  return (
    <div className="inline-flex flex-col">
      <Button
        variant="success"
        size="sm"
        isLoading={estaDescargando}
        loadingText="Generando Excel..."
        onClick={handleDescargar}
        leftIcon={<Download className="w-4 h-4" />}
      >
        Descargar Consolidado
      </Button>

      {error && (
        <span className="text-[11px] text-rose-600 mt-1 font-medium" role="alert">
          {error.getMensajeUsuario()}
        </span>
      )}
    </div>
  );
}
