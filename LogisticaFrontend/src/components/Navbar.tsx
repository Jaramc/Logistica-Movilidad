import React from 'react';
import { FileSpreadsheet, History } from 'lucide-react';

interface NavbarProps {
  vistaActiva: 'procesador' | 'historial';
  setVistaActiva: (vista: 'procesador' | 'historial') => void;
}

export const Navbar: React.FC<NavbarProps> = ({ vistaActiva, setVistaActiva }) => {
  return (
    <nav className="bg-slate-900 text-white px-6 py-3 flex items-center justify-between sticky top-0 z-50 shadow-md">
      <div className="flex items-center gap-4">
        {/* Logo L&M: Letras Azules con borde blanco */}
        <div className="flex items-center justify-center w-10 h-10 bg-slate-800 rounded-lg border-2 border-white overflow-hidden shadow-sm">
          <span className="text-blue-500 font-black text-xl tracking-tighter" style={{ WebkitTextStroke: '1px white' }}>
            L&M
          </span>
        </div>
        <div>
          <h1 className="font-bold tracking-wide text-sm">SISTEMA LOGÍSTICO</h1>
          <p className="text-xs text-slate-400">Logística & Movilidad</p>
        </div>
        <span className="ml-2 px-2.5 py-0.5 bg-blue-900/50 text-blue-400 text-xs font-medium rounded-full border border-blue-800">
          Azure AI OCR
        </span>
      </div>

      <div className="flex items-center gap-2">
        <button
          onClick={() => setVistaActiva('procesador')}
          className={`flex items-center gap-2 px-3.5 py-2 text-sm font-medium rounded-lg transition-colors ${
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
          className={`flex items-center gap-2 px-3.5 py-2 text-sm font-medium rounded-lg transition-colors border ${
            vistaActiva === 'historial'
              ? 'bg-slate-800 text-white border-blue-500 shadow-sm'
              : 'text-slate-300 hover:text-white hover:bg-slate-800 border-slate-700'
          }`}
        >
          <History className="w-4 h-4" />
          Historial Registrado
        </button>
      </div>
    </nav>
  );
};
