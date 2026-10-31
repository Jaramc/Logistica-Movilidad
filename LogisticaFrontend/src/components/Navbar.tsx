import React from 'react';
import { Truck, FileSpreadsheet, History } from 'lucide-react';

interface NavbarProps {
  vistaActiva: 'procesador' | 'historial';
  setVistaActiva: (vista: 'procesador' | 'historial') => void;
}

export const Navbar: React.FC<NavbarProps> = ({ vistaActiva, setVistaActiva }) => {
  return (
    <header className="bg-slate-900 text-white border-b border-slate-800 sticky top-0 z-50 shadow-md">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          <div className="flex items-center gap-3">
            <div className="bg-blue-600 p-2.5 rounded-xl text-white shadow-lg shadow-blue-500/30 flex items-center justify-center">
              <Truck className="w-6 h-6" />
            </div>
            <div>
              <span className="text-xs uppercase tracking-wider font-semibold text-blue-400">Sistema Logístico</span>
              <h1 className="text-lg font-bold tracking-tight text-white flex items-center gap-2">
                Logística & Movilidad
                <span className="text-xs px-2 py-0.5 rounded-full bg-blue-900/60 text-blue-300 border border-blue-700 font-mono">
                  Azure AI OCR
                </span>
              </h1>
            </div>
          </div>

          <nav className="flex items-center gap-2">
            <button
              onClick={() => setVistaActiva('procesador')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                vistaActiva === 'procesador'
                  ? 'bg-blue-600 text-white shadow-sm'
                  : 'text-slate-300 hover:text-white hover:bg-slate-800'
              }`}
            >
              <FileSpreadsheet className="w-4 h-4" />
              Digitalizar Liquidación
            </button>
            <button
              onClick={() => setVistaActiva('historial')}
              className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                vistaActiva === 'historial'
                  ? 'bg-blue-600 text-white shadow-sm'
                  : 'text-slate-300 hover:text-white hover:bg-slate-800'
              }`}
            >
              <History className="w-4 h-4" />
              Historial Registrado
            </button>
          </nav>
        </div>
      </div>
    </header>
  );
};
