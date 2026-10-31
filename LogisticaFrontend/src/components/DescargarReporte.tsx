import { useState } from 'react';
import { Download, RefreshCw } from 'lucide-react';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5098';

export default function DescargarReporte() {
  const [descargando, setDescargando] = useState(false);

  const exportarConsolidadoExcel = async () => {
    setDescargando(true);
    try {
      // Usamos API_BASE_URL (http://localhost:5098 o https://localhost:7216)
      const respuesta = await fetch(`${API_BASE_URL}/api/liquidaciones/descargar-consolidado`, {
        method: 'GET',
      });

      if (!respuesta.ok) {
        throw new Error('No hay datos para exportar o hubo un error en el servidor.');
      }

      // 1. Convertimos la respuesta HTTP a un Blob (archivo binario)
      const blob = await respuesta.blob();

      // 2. Creamos una URL temporal en memoria para el archivo
      const url = window.URL.createObjectURL(blob);

      // 3. Creamos un elemento <a> invisible para simular el clic de descarga
      const link = document.createElement('a');
      link.href = url;
      
      // Asignamos el nombre del archivo con la fecha de hoy
      const fecha = new Date().toISOString().split('T')[0];
      link.setAttribute('download', `Liquidaciones_Consolidadas_${fecha}.xlsx`);
      
      document.body.appendChild(link);
      link.click(); // Forzamos la descarga

      // 4. Limpiamos la memoria y el DOM
      link.parentNode?.removeChild(link);
      window.URL.revokeObjectURL(url);

    } catch (error) {
      console.error("Error al descargar:", error);
      alert('Hubo un problema al generar el Excel consolidado. Asegúrate de tener viajes aprobados.');
    } finally {
      setDescargando(false);
    }
  };

  return (
    <button 
      onClick={exportarConsolidadoExcel}
      disabled={descargando}
      className="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold py-2 px-4 rounded-xl shadow-xs transition-colors flex items-center gap-2 disabled:opacity-50 text-sm cursor-pointer"
    >
      {descargando ? (
        <>
          <RefreshCw className="w-4 h-4 animate-spin" />
          <span>Generando Excel...</span>
        </>
      ) : (
        <>
          <Download className="w-4 h-4" />
          <span>Descargar Consolidado</span>
        </>
      )}
    </button>
  );
}
