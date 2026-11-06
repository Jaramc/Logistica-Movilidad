import React, { useState, useEffect, useMemo } from 'react';
import { ZoomIn, ZoomOut, RotateCw, RefreshCw, FileText, ExternalLink } from 'lucide-react';

export interface VisorDocumentoProps {
  archivo: File | null;
  onCambiarArchivo?: () => void;
}

export const VisorDocumento: React.FC<VisorDocumentoProps> = ({
  archivo,
  onCambiarArchivo,
}) => {
  const [zoom, setZoom] = useState(1);
  const [rotacion, setRotacion] = useState(0);

  // Derivación de Object URL mediante useMemo con limpieza estricta en unmount
  const previewUrl = useMemo(() => {
    if (!archivo) return null;
    return URL.createObjectURL(archivo);
  }, [archivo]);

  useEffect(() => {
    return () => {
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
      }
    };
  }, [previewUrl]);

  if (!archivo || !previewUrl) {
    return (
      <div className="h-full flex flex-col items-center justify-center p-8 text-center bg-slate-50 border border-slate-200 rounded-2xl">
        <FileText className="w-12 h-12 text-slate-300 mb-3" aria-hidden="true" />
        <p className="text-sm font-semibold text-slate-600">No hay documento cargado</p>
        <p className="text-xs text-slate-400 mt-1">Sube una planilla para ver la inspección en vivo.</p>
      </div>
    );
  }

  const esPdf = archivo.type === 'application/pdf' || archivo.name.toLowerCase().endsWith('.pdf');

  return (
    <div className="relative w-full h-full flex flex-col bg-slate-900/5 rounded-2xl border border-slate-200 overflow-hidden">
      {/* Barra de herramientas flotante del visor */}
      <div className="absolute top-3 right-3 z-10 bg-white/95 backdrop-blur-md border border-slate-200 shadow-md rounded-xl p-1 flex items-center gap-1">
        <button
          type="button"
          onClick={() => setZoom((z) => Math.max(0.5, Number((z - 0.2).toFixed(1))))}
          className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition"
          title="Reducir Zoom"
          aria-label="Reducir Zoom"
        >
          <ZoomOut className="w-4 h-4" />
        </button>

        <span className="text-xs font-mono font-semibold px-1 text-slate-700 min-w-[45px] text-center select-none">
          {Math.round(zoom * 100)}%
        </span>

        <button
          type="button"
          onClick={() => setZoom((z) => Math.min(3.5, Number((z + 0.2).toFixed(1))))}
          className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition"
          title="Aumentar Zoom"
          aria-label="Aumentar Zoom"
        >
          <ZoomIn className="w-4 h-4" />
        </button>

        <button
          type="button"
          onClick={() => setRotacion((r) => (r + 90) % 360)}
          className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition"
          title="Rotar 90 grados"
          aria-label="Rotar 90 grados"
        >
          <RotateCw className="w-4 h-4" />
        </button>

        <button
          type="button"
          onClick={() => {
            setZoom(1);
            setRotacion(0);
          }}
          className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition"
          title="Restablecer vista"
          aria-label="Restablecer vista"
        >
          <RefreshCw className="w-4 h-4" />
        </button>

        {onCambiarArchivo && (
          <>
            <div className="w-px h-4 bg-slate-200 mx-0.5" />
            <button
              type="button"
              onClick={onCambiarArchivo}
              className="text-xs font-semibold px-2 py-1 text-blue-600 hover:bg-blue-50 rounded-lg transition"
            >
              Cambiar
            </button>
          </>
        )}
      </div>

      {/* Contenedor del documento */}
      <div className="w-full h-full overflow-auto flex items-center justify-center p-4">
        {esPdf ? (
          <div className="w-full h-full flex flex-col items-center justify-center bg-white rounded-xl shadow-xs p-6 text-center">
            <FileText className="w-16 h-16 text-rose-500 mb-3" />
            <h4 className="font-bold text-slate-800 text-sm mb-1">{archivo.name}</h4>
            <p className="text-xs text-slate-500 mb-4">Documento PDF cargado para extracción OCR</p>
            <a
              href={previewUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1.5 px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl transition"
            >
              <span>Abrir PDF en pestaña nueva</span>
              <ExternalLink className="w-3.5 h-3.5" />
            </a>
          </div>
        ) : (
          <img
            src={previewUrl}
            alt="Comprobante físico de la planilla cargado"
            style={{
              transform: `scale(${zoom}) rotate(${rotacion}deg)`,
              transformOrigin: 'center center',
              transition: 'transform 0.15s ease-out',
            }}
            className="max-h-full max-w-full object-contain rounded-lg shadow-sm"
          />
        )}
      </div>
    </div>
  );
};
