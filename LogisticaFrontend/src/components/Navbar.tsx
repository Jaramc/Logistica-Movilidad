import React from 'react';
import { FileSpreadsheet, History, LogOut, ScrollText, Users } from 'lucide-react';
import type { Vista } from '../features/auth/permisos';

interface NavbarProps {
  vistas: Vista[];
  vistaActiva: Vista;
  setVistaActiva: (vista: Vista) => void;
  nombreUsuario: string;
  rol: string;
  onCerrarSesion: () => void;
}

const ETIQUETAS: Record<Vista, { texto: string; icono: React.ReactNode }> = {
  procesador: { texto: 'Digitalizar Liquidación', icono: <FileSpreadsheet className="w-4 h-4" /> },
  historial: { texto: 'Historial', icono: <History className="w-4 h-4" /> },
  usuarios: { texto: 'Usuarios', icono: <Users className="w-4 h-4" /> },
  auditoria: { texto: 'Auditoría', icono: <ScrollText className="w-4 h-4" /> },
};

/** El menú solo muestra lo que el rol puede usar. Es comodidad: el backend valida cada petición. */
export const Navbar: React.FC<NavbarProps> = ({ vistas, vistaActiva, setVistaActiva, nombreUsuario, rol, onCerrarSesion }) => {
  return (
    <nav className="bg-slate-900 text-white px-6 py-3 flex flex-wrap items-center justify-between gap-3 sticky top-0 z-50 shadow-md">
      <div className="flex items-center gap-4">
        <div className="flex items-center justify-center w-10 h-10 bg-slate-800 rounded-lg border-2 border-white overflow-hidden shadow-sm">
          <span className="text-blue-500 font-black text-xl tracking-tighter" style={{ WebkitTextStroke: '1px white' }}>
            L&M
          </span>
        </div>
        <div>
          <h1 className="font-bold tracking-wide text-sm">SISTEMA LOGÍSTICO</h1>
          <p className="text-xs text-slate-400">Logística & Movilidad</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2" role="navigation" aria-label="Menú principal">
        {vistas.map((vista) => (
          <button
            key={vista}
            onClick={() => setVistaActiva(vista)}
            aria-current={vistaActiva === vista ? 'page' : undefined}
            className={`flex items-center gap-2 px-3.5 py-2 text-sm font-medium rounded-lg transition-colors ${
              vistaActiva === vista ? 'bg-blue-600 text-white shadow-sm' : 'text-slate-300 hover:text-white hover:bg-slate-800'
            }`}
          >
            {ETIQUETAS[vista].icono}
            {ETIQUETAS[vista].texto}
          </button>
        ))}

        <div className="flex items-center gap-3 ml-4 pl-4 border-l border-slate-700">
          <div className="text-right">
            <p className="text-xs font-medium">{nombreUsuario}</p>
            <p className="text-[11px] text-slate-400">{rol}</p>
          </div>
          <button
            onClick={onCerrarSesion}
            className="flex items-center gap-1.5 px-3 py-2 text-sm text-slate-300 hover:text-white hover:bg-slate-800 rounded-lg"
          >
            <LogOut className="w-4 h-4" />
            Cerrar sesión
          </button>
        </div>
      </div>
    </nav>
  );
};
