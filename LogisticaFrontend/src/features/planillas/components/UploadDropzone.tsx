import React, { useRef, useState } from 'react';
import { UploadCloud, FileText, AlertCircle, Camera } from 'lucide-react';
import { validarArchivoPlanilla, type ResultadoValidacionArchivo } from '../utils/fileValidation';
import { cn } from '../../../lib/utils';

export interface UploadDropzoneProps {
  onArchivoAceptado: (archivo: File, validacion: ResultadoValidacionArchivo) => void;
  disabled?: boolean;
}

export const UploadDropzone: React.FC<UploadDropzoneProps> = ({
  onArchivoAceptado,
  disabled = false,
}) => {
  const [arrastrando, setArrastrando] = useState(false);
  const [errorLocal, setErrorLocal] = useState<string | null>(null);
  const [validando, setValidando] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const cameraInputRef = useRef<HTMLInputElement>(null);

  const procesarArchivo = async (file: File) => {
    setErrorLocal(null);
    setValidando(true);

    try {
      // Validación previa en cliente de tamaño y Magic Bytes reales
      const resultado = await validarArchivoPlanilla(file);
      if (!resultado.esValido) {
        setErrorLocal(resultado.error || 'Archivo inválido');
        return;
      }

      onArchivoAceptado(file, resultado);
    } catch {
      setErrorLocal('Ocurrió un error al verificar el archivo seleccionado.');
    } finally {
      setValidando(false);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    if (disabled) return;
    setArrastrando(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setArrastrando(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setArrastrando(false);
    if (disabled) return;

    const file = e.dataTransfer.files?.[0];
    if (file) {
      await procesarArchivo(file);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (disabled) return;
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      fileInputRef.current?.click();
    }
  };

  return (
    <div className="w-full">
      {/* Inputs nativos ocultos */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".jpg,.jpeg,.png,.pdf"
        disabled={disabled || validando}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) procesarArchivo(file);
          e.target.value = ''; // Permite volver a seleccionar el mismo archivo
        }}
        className="hidden"
        aria-hidden="true"
      />

      <input
        ref={cameraInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        disabled={disabled || validando}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) procesarArchivo(file);
          e.target.value = '';
        }}
        className="hidden"
        aria-hidden="true"
      />

      <div
        role="button"
        tabIndex={disabled ? -1 : 0}
        aria-label="Cargar soporte o comprobante de viaje. Arrastra un archivo o presiona enter para buscar."
        aria-disabled={disabled || validando}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => !disabled && !validando && fileInputRef.current?.click()}
        onKeyDown={handleKeyDown}
        className={cn(
          'border-2 border-dashed rounded-2xl p-8 sm:p-12 transition-all flex flex-col items-center justify-center text-center cursor-pointer select-none outline-none focus:ring-4 focus:ring-blue-100',
          arrastrando
            ? 'border-blue-500 bg-blue-50/60 scale-[1.01]'
            : 'border-slate-300 hover:border-blue-400 bg-slate-50/50 hover:bg-blue-50/30',
          disabled && 'opacity-50 cursor-not-allowed hover:border-slate-300 hover:bg-slate-50/50'
        )}
      >
        <div className="w-16 h-16 bg-white rounded-2xl shadow-xs border border-slate-200 flex items-center justify-center mb-4 text-blue-600 transition-transform group-hover:scale-110">
          <UploadCloud className="w-8 h-8" aria-hidden="true" />
        </div>

        <h3 className="font-bold text-slate-800 text-base sm:text-lg mb-1">
          {validando ? 'Comprobando integridad del archivo...' : 'Arrastra la planilla o comprobante'}
        </h3>
        <p className="text-sm text-slate-500 max-w-sm mb-4">
          o haz clic en cualquier lugar para explorar los archivos de tu equipo
        </p>

        {/* Botones de acción complementaria */}
        <div className="flex flex-wrap items-center justify-center gap-2 mb-4">
          <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white border border-slate-200 rounded-full text-xs font-medium text-slate-600 shadow-2xs">
            <FileText className="w-3.5 h-3.5 text-blue-500" />
            PDF, JPG o PNG hasta 10 MB
          </span>
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              cameraInputRef.current?.click();
            }}
            className="sm:hidden inline-flex items-center gap-1.5 px-3 py-1 bg-blue-50 border border-blue-200 rounded-full text-xs font-semibold text-blue-700 active:bg-blue-100"
          >
            <Camera className="w-3.5 h-3.5" />
            Tomar foto
          </button>
        </div>

        <span className="text-xs text-slate-400">
          Verificación automática de Magic Bytes antes de procesar
        </span>
      </div>

      {errorLocal && (
        <div className="mt-3 p-3 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl text-xs flex items-center gap-2" role="alert">
          <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
          <span>{errorLocal}</span>
        </div>
      )}
    </div>
  );
};

