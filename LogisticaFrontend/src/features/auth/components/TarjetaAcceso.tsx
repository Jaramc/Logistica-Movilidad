import type { ReactNode } from 'react';

/** Marco común de las pantallas de acceso. */
export function TarjetaAcceso({ titulo, subtitulo, children }: { titulo: string; subtitulo?: string; children: ReactNode }) {
  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center px-4 py-10">
      <div className="w-full max-w-md bg-white border border-slate-200 rounded-2xl shadow-sm p-8">
        <div className="flex items-center gap-3 mb-6">
          <div className="flex items-center justify-center w-10 h-10 bg-slate-900 rounded-lg">
            <span className="text-blue-400 font-black text-lg tracking-tighter">L&M</span>
          </div>
          <div>
            <h1 className="text-lg font-bold text-slate-900">{titulo}</h1>
            {subtitulo && <p className="text-xs text-slate-500">{subtitulo}</p>}
          </div>
        </div>
        {children}
      </div>
    </div>
  );
}
