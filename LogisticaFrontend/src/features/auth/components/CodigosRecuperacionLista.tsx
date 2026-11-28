import { Button } from '../../../components/ui/Button';
import { Alert } from '../../../components/ui/Alert';

/** Muestra los códigos UNA sola vez. Se pueden descargar como texto; nunca se guardan en el navegador. */
export function CodigosRecuperacionLista({ codigos, onContinuar }: { codigos: string[]; onContinuar: () => void }) {
  const descargar = () => {
    const contenido = ['Códigos de recuperación — Logística & Movilidad', 'Cada código sirve una sola vez.', '', ...codigos].join('\n');
    const url = URL.createObjectURL(new Blob([contenido], { type: 'text/plain;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'codigos-recuperacion.txt';
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  return (
    <div className="space-y-4">
      <Alert variant="warning" title="Guárdelos ahora">
        Estos códigos se muestran una sola vez. Úselos si pierde el teléfono; cada uno sirve una vez.
      </Alert>
      <ul className="grid grid-cols-2 gap-2 font-mono text-sm bg-slate-50 border border-slate-200 rounded-lg p-4" aria-label="Códigos de recuperación">
        {codigos.map((c) => (
          <li key={c} className="text-slate-800">
            {c}
          </li>
        ))}
      </ul>
      <div className="flex gap-2">
        <Button variant="outline" onClick={descargar} className="flex-1">
          Descargar .txt
        </Button>
        <Button onClick={onContinuar} className="flex-1">
          Ya los guardé
        </Button>
      </div>
    </div>
  );
}
