import { useState } from 'react';
import { Download } from 'lucide-react';
import { useDescargarConsolidado } from '../features/planillas/hooks/useDescargarConsolidado';
import { Button } from './ui/Button';
import { hoyIso, validarRango } from '../features/planillas/utils/rangoFechas';

export default function DescargarReporte() {
  const { descargar, estaDescargando, error } = useDescargarConsolidado();
  const hoy = hoyIso();
  const [desde, setDesde] = useState(`${hoy.slice(0, 4)}-01-01`);
  const [hasta, setHasta] = useState(hoy);
  const errorRango = validarRango(desde, hasta, hoy);

  const handleDescargar = async () => {
    if (errorRango) return;
    try {
      await descargar({ desde, hasta });
    } catch {
      // Error expuesto por useDescargarConsolidado
    }
  };

  return (
    <div className="inline-flex flex-col gap-1">
      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-col text-[11px] font-medium text-slate-600">
          Desde
          <input
            type="date"
            value={desde}
            max={hoy}
            onChange={(e) => setDesde(e.target.value)}
            className="mt-0.5 rounded-md border border-slate-300 px-2 py-1 text-xs"
          />
        </label>
        <label className="flex flex-col text-[11px] font-medium text-slate-600">
          Hasta
          <input
            type="date"
            value={hasta}
            max={hoy}
            onChange={(e) => setHasta(e.target.value)}
            className="mt-0.5 rounded-md border border-slate-300 px-2 py-1 text-xs"
          />
        </label>
        <Button
          variant="success"
          size="sm"
          isLoading={estaDescargando}
          loadingText="Generando Excel..."
          onClick={handleDescargar}
          disabled={errorRango !== null}
          leftIcon={<Download className="w-4 h-4" />}
        >
          Descargar Consolidado
        </Button>
      </div>

      {(errorRango || error) && (
        <span className="text-[11px] text-rose-600 font-medium" role="alert">
          {errorRango ?? error?.getMensajeUsuario()}
        </span>
      )}
    </div>
  );
}
