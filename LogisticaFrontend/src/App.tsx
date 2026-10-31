import { useState } from 'react';
import { Navbar } from './components/Navbar';
import ProcesadorLiquidaciones from './components/ProcesadorLiquidaciones';
import { HistorialLiquidaciones } from './components/HistorialLiquidaciones';

function App() {
  const [vistaActiva, setVistaActiva] = useState<'procesador' | 'historial'>('procesador');

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <Navbar vistaActiva={vistaActiva} setVistaActiva={setVistaActiva} />
      
      <main className="flex-1">
        {vistaActiva === 'procesador' ? (
          <ProcesadorLiquidaciones />
        ) : (
          <HistorialLiquidaciones />
        )}
      </main>

      <footer className="bg-white border-t border-slate-200 py-4 text-center text-xs text-slate-500">
        Logística & Movilidad • Módulo de Liquidación con Azure AI Document Analysis y PostgreSQL
      </footer>
    </div>
  );
}

export default App;
