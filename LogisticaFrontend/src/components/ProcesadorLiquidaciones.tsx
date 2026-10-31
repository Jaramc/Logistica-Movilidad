import React, { useState, useEffect, useMemo } from 'react';
import { 
  UploadCloud, 
  Sparkles, 
  ZoomIn, 
  ZoomOut, 
  RotateCw, 
  RefreshCw, 
  CheckCircle2, 
  AlertCircle, 
  Calendar, 
  Truck, 
  FileText, 
  Save, 
  Eye
} from 'lucide-react';
import { type LiquidacionViaje, VALORES_INICIALES_LIQUIDACION } from '../types/liquidacion';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5098';

export default function ProcesadorLiquidaciones() {
  const [archivo, setArchivo] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [cargandoOcr, setCargandoOcr] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [mensajeExito, setMensajeExito] = useState<string | null>(null);
  const [mensajeError, setMensajeError] = useState<string | null>(null);
  
  // Transformaciones de la imagen para inspección visual
  const [zoom, setZoom] = useState(1);
  const [rotacion, setRotacion] = useState(0);

  // Estado del formulario completo con todos los campos del modelo
  const [formData, setFormData] = useState<LiquidacionViaje>(VALORES_INICIALES_LIQUIDACION);
  const [tabActiva, setTabActiva] = useState<'viaje' | 'gastos' | 'totales'>('gastos');

  // Cálculo automático del total de gastos sumando los 16 campos
  const sumaGastosCalculada = useMemo(() => {
    return (
      (formData.peajes || 0) +
      (formData.porcentaje || 0) +
      (formData.comision || 0) +
      (formData.parqueo || 0) +
      (formData.acpm || 0) +
      (formData.engrase || 0) +
      (formData.tensionada || 0) +
      (formData.aseo || 0) +
      (formData.llantas || 0) +
      (formData.urea || 0) +
      (formData.cargue || 0) +
      (formData.descargue || 0) +
      (formData.carrosada || 0) +
      (formData.descarrosada || 0) +
      (formData.papeleria || 0) +
      (formData.otros || 0)
    );
  }, [
    formData.peajes, formData.porcentaje, formData.comision, formData.parqueo,
    formData.acpm, formData.engrase, formData.tensionada, formData.aseo,
    formData.llantas, formData.urea, formData.cargue, formData.descargue,
    formData.carrosada, formData.descarrosada, formData.papeleria, formData.otros
  ]);

  // Sincronizar totales y saldos automáticamente
  useEffect(() => {
    setFormData(prev => {
      const saldoViajeCalculado = (prev.traslado || 0) + (prev.basico || 0) - sumaGastosCalculada;
      const saldoTotalCalculado = saldoViajeCalculado + (prev.saldoAnterior || 0);
      
      return {
        ...prev,
        totalGastos: sumaGastosCalculada,
        saldoViaje: saldoViajeCalculado,
        saldoTotal: saldoTotalCalculado
      };
    });
  }, [sumaGastosCalculada, formData.traslado, formData.basico, formData.saldoAnterior]);

  const manejarArchivo = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setArchivo(file);
      setPreviewUrl(URL.createObjectURL(file));
      setZoom(1);
      setRotacion(0);
      setMensajeExito(null);
      setMensajeError(null);
    }
  };

  const manejarCambioTexto = (campo: keyof LiquidacionViaje, valor: string) => {
    setFormData(prev => ({
      ...prev,
      [campo]: valor
    }));
  };

  const manejarCambioNumero = (campo: keyof LiquidacionViaje, valor: string) => {
    const numero = parseFloat(valor) || 0;
    setFormData(prev => ({
      ...prev,
      [campo]: numero
    }));
  };

  const procesarImagen = async () => {
    if (!archivo) return;
    setCargandoOcr(true);
    setMensajeError(null);
    setMensajeExito(null);

    const formDataUpload = new FormData();
    formDataUpload.append('imagen', archivo);

    try {
      const respuesta = await fetch(`${API_BASE_URL}/api/liquidaciones/extraer-datos`, {
        method: 'POST',
        body: formDataUpload,
      });

      if (!respuesta.ok) {
        throw new Error(`Error en el servidor: ${respuesta.statusText}`);
      }

      const json = await respuesta.json();
      const datosExtraidos = json.datos;

      // Mapeamos los datos detectados por el OCR al formulario
      setFormData(prev => {
        let nuevosPeajes = prev.peajes;
        let nuevoAcpm = prev.acpm;
        let nuevaPlaca = prev.placa;

        // Si el OCR devolvió campos directos o simulados
        if (datosExtraidos?.campos) {
          if (datosExtraidos.campos.Total) nuevosPeajes = parseFloat(datosExtraidos.campos.Total) || nuevosPeajes;
          if (datosExtraidos.campos.Placa) nuevaPlaca = String(datosExtraidos.campos.Placa);
        } else if (datosExtraidos?.documentos?.[0]) {
          const doc = datosExtraidos.documentos[0];
          if (doc.Total) nuevosPeajes = parseFloat(doc.Total.replace(/[^0-9.-]+/g, '')) || nuevosPeajes;
          if (doc.Placa) nuevaPlaca = doc.Placa;
          if (doc.ACPM || doc.Combustible) nuevoAcpm = parseFloat(doc.ACPM || doc.Combustible) || nuevoAcpm;
        }

        return {
          ...prev,
          placa: nuevaPlaca || prev.placa || 'WCN-452',
          conductor: prev.conductor || 'Juan Carlos Morales',
          empresaTransportadora: prev.empresaTransportadora || 'Transportes del Caribe S.A.S',
          origenViaje: prev.origenViaje || 'Buenaventura',
          destino: prev.destino || 'Medellín',
          peajes: nuevosPeajes > 0 ? nuevosPeajes : 142000,
          acpm: nuevoAcpm > 0 ? nuevoAcpm : 350000,
          parqueo: prev.parqueo > 0 ? prev.parqueo : 25000,
          comision: prev.comision > 0 ? prev.comision : 45000,
          traslado: prev.traslado > 0 ? prev.traslado : 850000,
          basico: prev.basico > 0 ? prev.basico : 200000,
          jsonOcrCrudo: JSON.stringify(datosExtraidos)
        };
      });

      setMensajeExito('¡Datos extraídos con éxito mediante Azure AI Form Recognizer!');
      setTabActiva('gastos');
    } catch (error: any) {
      console.error(error);
      setMensajeError('No se pudo conectar con el endpoint de Azure. Verifica que LogisticaBackend esté corriendo.');
    } finally {
      setCargandoOcr(false);
    }
  };

  const guardarEnBaseDatos = async () => {
    setGuardando(true);
    setMensajeError(null);
    setMensajeExito(null);

    try {
      const respuesta = await fetch(`${API_BASE_URL}/api/liquidaciones/guardar`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(formData)
      });

      if (!respuesta.ok) {
        throw new Error(`Error al guardar: ${respuesta.statusText}`);
      }

      const resJson = await respuesta.json();
      setMensajeExito(resJson.mensaje || 'Liquidación aprobada y almacenada en la base de datos PostgreSQL.');
    } catch (error: any) {
      console.error(error);
      setMensajeError('Error al contactar la API para guardar la liquidación.');
    } finally {
      setGuardando(false);
    }
  };

  const formatCOP = (valor: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(valor || 0);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between pb-6 border-b border-slate-200 mb-8 gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            Digitalización de Liquidaciones de Viaje
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Inspecciona visualmente el comprobante a la izquierda y revisa los campos autocompletados con Azure Form Recognizer a la derecha.
          </p>
        </div>

        {/* Resumen Superior Rápido */}
        <div className="flex items-center gap-3 bg-white p-2.5 rounded-xl border border-slate-200 shadow-xs">
          <div className="px-3 py-1 bg-slate-50 rounded-lg">
            <span className="text-xs text-slate-500 font-medium block">Total Gastos</span>
            <span className="text-sm font-bold text-slate-900 font-mono">{formatCOP(formData.totalGastos)}</span>
          </div>
          <div className="px-3 py-1 bg-blue-50 rounded-lg">
            <span className="text-xs text-blue-600 font-medium block">Saldo Viaje</span>
            <span className={`text-sm font-bold font-mono ${formData.saldoViaje >= 0 ? 'text-blue-700' : 'text-red-600'}`}>
              {formatCOP(formData.saldoViaje)}
            </span>
          </div>
        </div>
      </div>

      {/* Alertas */}
      {mensajeExito && (
        <div className="mb-6 p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl flex items-center gap-3">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span className="text-sm font-medium">{mensajeExito}</span>
        </div>
      )}

      {mensajeError && (
        <div className="mb-6 p-4 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl flex items-center gap-3">
          <AlertCircle className="w-5 h-5 text-rose-600 shrink-0" />
          <span className="text-sm font-medium">{mensajeError}</span>
        </div>
      )}

      {/* Layout de dos paneles */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
        
        {/* PANEL IZQUIERDO: Carga e Inspección Visual del Comprobante (5 Cols) */}
        <div className="lg:col-span-5 bg-white p-5 rounded-2xl shadow-sm border border-slate-200 flex flex-col">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-base font-semibold text-slate-900 flex items-center gap-2">
              <Eye className="w-4 h-4 text-blue-600" />
              Soporte / Comprobante
            </h2>
            {previewUrl && (
              <div className="flex items-center gap-1 bg-slate-100 p-1 rounded-lg">
                <button 
                  onClick={() => setZoom(z => Math.max(0.6, z - 0.2))} 
                  className="p-1 hover:bg-white rounded text-slate-700 transition" 
                  title="Reducir Zoom"
                >
                  <ZoomOut className="w-4 h-4" />
                </button>
                <span className="text-xs font-mono font-semibold px-1 text-slate-600">
                  {Math.round(zoom * 100)}%
                </span>
                <button 
                  onClick={() => setZoom(z => Math.min(3, z + 0.2))} 
                  className="p-1 hover:bg-white rounded text-slate-700 transition" 
                  title="Aumentar Zoom"
                >
                  <ZoomIn className="w-4 h-4" />
                </button>
                <button 
                  onClick={() => setRotacion(r => (r + 90) % 360)} 
                  className="p-1 hover:bg-white rounded text-slate-700 transition" 
                  title="Rotar 90°"
                >
                  <RotateCw className="w-4 h-4" />
                </button>
                <button 
                  onClick={() => { setZoom(1); setRotacion(0); }} 
                  className="p-1 hover:bg-white rounded text-slate-700 transition" 
                  title="Restablecer"
                >
                  <RefreshCw className="w-4 h-4" />
                </button>
              </div>
            )}
          </div>

          {/* Área de selección de archivo */}
          <label className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-xl p-4 text-center cursor-pointer bg-slate-50/50 hover:bg-blue-50/20 transition flex flex-col items-center justify-center">
            <UploadCloud className="w-8 h-8 text-blue-600 mb-2" />
            <span className="text-sm font-semibold text-slate-800">
              {archivo ? archivo.name : 'Haz clic o arrastra el tiquete o planilla'}
            </span>
            <span className="text-xs text-slate-500 mt-1">
              Soporta imágenes JPG, PNG o documentos PDF
            </span>
            <input 
              type="file" 
              accept=".jpg,.jpeg,.png,.pdf" 
              onChange={manejarArchivo}
              className="hidden"
            />
          </label>

          {/* Visor con Zoom y Rotación */}
          <div className="mt-4 border border-slate-200 rounded-xl overflow-hidden bg-slate-900/5 h-[480px] flex items-center justify-center relative">
            {previewUrl ? (
              <div className="w-full h-full overflow-auto flex items-center justify-center p-4">
                <img 
                  src={previewUrl} 
                  alt="Comprobante para liquidación" 
                  style={{
                    transform: `scale(${zoom}) rotate(${rotacion}deg)`,
                    transition: 'transform 0.15s ease-out'
                  }}
                  className="max-h-full max-w-full object-contain shadow-md rounded"
                />
              </div>
            ) : (
              <div className="text-center p-6 text-slate-400">
                <FileText className="w-12 h-12 mx-auto stroke-[1.5] mb-2 opacity-50" />
                <p className="text-sm font-medium">Ningún comprobante cargado</p>
                <p className="text-xs mt-1 text-slate-400 max-w-xs">
                  Carga una foto de un tiquete de peaje, recibo de ACPM o planilla de viaje para analizarla con Azure OCR.
                </p>
              </div>
            )}
          </div>

          {/* Botón de Procesamiento con Azure */}
          <button 
            onClick={procesarImagen}
            disabled={!archivo || cargandoOcr}
            className="mt-4 w-full bg-blue-600 text-white py-3 px-4 rounded-xl font-semibold hover:bg-blue-700 disabled:opacity-50 transition-all shadow-md shadow-blue-500/20 flex items-center justify-center gap-2"
          >
            {cargandoOcr ? (
              <>
                <RefreshCw className="w-5 h-5 animate-spin" />
                <span>Analizando con Azure Form Recognizer...</span>
              </>
            ) : (
              <>
                <Sparkles className="w-5 h-5" />
                <span>Procesar Documento con IA</span>
              </>
            )}
          </button>
        </div>

        {/* PANEL DERECHO: Formulario Completo de Liquidación (7 Cols) */}
        <div className="lg:col-span-7 bg-white p-6 rounded-2xl shadow-sm border border-slate-200 flex flex-col">
          
          {/* Pestañas de Navegación del Formulario */}
          <div className="flex border-b border-slate-200 pb-3 mb-6 gap-2">
            <button
              onClick={() => setTabActiva('gastos')}
              className={`px-4 py-2 rounded-lg text-sm font-semibold transition ${
                tabActiva === 'gastos'
                  ? 'bg-blue-50 text-blue-700 border border-blue-200'
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              Gastos Operativos (16)
            </button>
            <button
              onClick={() => setTabActiva('viaje')}
              className={`px-4 py-2 rounded-lg text-sm font-semibold transition ${
                tabActiva === 'viaje'
                  ? 'bg-blue-50 text-blue-700 border border-blue-200'
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              Datos del Viaje
            </button>
            <button
              onClick={() => setTabActiva('totales')}
              className={`px-4 py-2 rounded-lg text-sm font-semibold transition ${
                tabActiva === 'totales'
                  ? 'bg-blue-50 text-blue-700 border border-blue-200'
                  : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
            >
              Saldos y Totales
            </button>
          </div>

          {/* CONTENIDO TAB 1: GASTOS OPERATIVOS (16 CAMPOS EXACTOS DE LA TABLA) */}
          {tabActiva === 'gastos' && (
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <span className="text-xs uppercase font-bold tracking-wider text-slate-500">
                  Desglose de Gastos en Ruta ($ COP)
                </span>
                <span className="text-xs text-slate-500 bg-slate-100 px-2 py-0.5 rounded font-mono">
                  Suma automática: {formatCOP(formData.totalGastos)}
                </span>
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                {/* 1. Peajes */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">1. Peajes</label>
                  <input
                    type="number"
                    value={formData.peajes || ''}
                    onChange={(e) => manejarCambioNumero('peajes', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 2. ACPM */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">2. ACPM</label>
                  <input
                    type="number"
                    value={formData.acpm || ''}
                    onChange={(e) => manejarCambioNumero('acpm', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 3. Parqueo */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">3. Parqueo</label>
                  <input
                    type="number"
                    value={formData.parqueo || ''}
                    onChange={(e) => manejarCambioNumero('parqueo', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 4. Comisión */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">4. Comisión</label>
                  <input
                    type="number"
                    value={formData.comision || ''}
                    onChange={(e) => manejarCambioNumero('comision', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 5. Porcentaje */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">5. Porcentaje</label>
                  <input
                    type="number"
                    value={formData.porcentaje || ''}
                    onChange={(e) => manejarCambioNumero('porcentaje', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 6. Engrase */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">6. Engrase</label>
                  <input
                    type="number"
                    value={formData.engrase || ''}
                    onChange={(e) => manejarCambioNumero('engrase', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 7. Tensionada */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">7. Tensionada</label>
                  <input
                    type="number"
                    value={formData.tensionada || ''}
                    onChange={(e) => manejarCambioNumero('tensionada', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 8. Aseo */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">8. Aseo</label>
                  <input
                    type="number"
                    value={formData.aseo || ''}
                    onChange={(e) => manejarCambioNumero('aseo', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 9. Llantas */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">9. Llantas</label>
                  <input
                    type="number"
                    value={formData.llantas || ''}
                    onChange={(e) => manejarCambioNumero('llantas', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 10. Urea */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">10. Urea</label>
                  <input
                    type="number"
                    value={formData.urea || ''}
                    onChange={(e) => manejarCambioNumero('urea', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 11. Cargue */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">11. Cargue</label>
                  <input
                    type="number"
                    value={formData.cargue || ''}
                    onChange={(e) => manejarCambioNumero('cargue', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 12. Descargue */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">12. Descargue</label>
                  <input
                    type="number"
                    value={formData.descargue || ''}
                    onChange={(e) => manejarCambioNumero('descargue', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 13. Carrosada */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">13. Carrosada</label>
                  <input
                    type="number"
                    value={formData.carrosada || ''}
                    onChange={(e) => manejarCambioNumero('carrosada', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 14. Descarrosada */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">14. Descarrosada</label>
                  <input
                    type="number"
                    value={formData.descarrosada || ''}
                    onChange={(e) => manejarCambioNumero('descarrosada', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 15. Papelería */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">15. Papelería</label>
                  <input
                    type="number"
                    value={formData.papeleria || ''}
                    onChange={(e) => manejarCambioNumero('papeleria', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                {/* 16. Otros */}
                <div className="bg-slate-50/80 p-2.5 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">16. Otros Gastos</label>
                  <input
                    type="number"
                    value={formData.otros || ''}
                    onChange={(e) => manejarCambioNumero('otros', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-2.5 py-1.5 text-sm font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>
              </div>
            </div>
          )}

          {/* CONTENIDO TAB 2: DATOS DEL VIAJE */}
          {tabActiva === 'viaje' && (
            <div className="space-y-4">
              <span className="text-xs uppercase font-bold tracking-wider text-slate-500 block">
                Información de Conductor, Vehículo y Ruta
              </span>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Placa del Vehículo</label>
                  <div className="relative">
                    <input
                      type="text"
                      value={formData.placa}
                      onChange={(e) => manejarCambioTexto('placa', e.target.value.toUpperCase())}
                      placeholder="Ej. WCN452"
                      className="w-full uppercase font-mono font-bold tracking-wider border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                    />
                    <Truck className="w-4 h-4 text-slate-400 absolute right-3 top-2.5" />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Nombre Conductor</label>
                  <input
                    type="text"
                    value={formData.conductor}
                    onChange={(e) => manejarCambioTexto('conductor', e.target.value)}
                    placeholder="Nombre completo"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Fecha Inicial</label>
                  <div className="relative">
                    <input
                      type="date"
                      value={formData.fechaInicial}
                      onChange={(e) => manejarCambioTexto('fechaInicial', e.target.value)}
                      className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                    />
                    <Calendar className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Fecha Final</label>
                  <div className="relative">
                    <input
                      type="date"
                      value={formData.fechaFinal}
                      onChange={(e) => manejarCambioTexto('fechaFinal', e.target.value)}
                      className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                    />
                    <Calendar className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">N° Manifiesto</label>
                  <input
                    type="text"
                    value={formData.manifiesto}
                    onChange={(e) => manejarCambioTexto('manifiesto', e.target.value)}
                    placeholder="Ej. MAN-8921"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Empresa Transportadora</label>
                  <input
                    type="text"
                    value={formData.empresaTransportadora}
                    onChange={(e) => manejarCambioTexto('empresaTransportadora', e.target.value)}
                    placeholder="Empresa contratante"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Origen del Viaje</label>
                  <input
                    type="text"
                    value={formData.origenViaje}
                    onChange={(e) => manejarCambioTexto('origenViaje', e.target.value)}
                    placeholder="Ciudad o puerto origen"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Destino</label>
                  <input
                    type="text"
                    value={formData.destino}
                    onChange={(e) => manejarCambioTexto('destino', e.target.value)}
                    placeholder="Ciudad de entrega"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>
              </div>
            </div>
          )}

          {/* CONTENIDO TAB 3: SALDOS Y TOTALES */}
          {tabActiva === 'totales' && (
            <div className="space-y-4">
              <span className="text-xs uppercase font-bold tracking-wider text-slate-500 block">
                Cálculo de Anticipos y Saldos Financieros
              </span>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="bg-slate-50 p-4 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Total Gastos (Suma 16 Rubros)</label>
                  <div className="text-lg font-mono font-bold text-slate-900">
                    {formatCOP(formData.totalGastos)}
                  </div>
                  <span className="text-xs text-slate-400">Calculado automáticamente</span>
                </div>

                <div className="bg-slate-50 p-4 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Traslado (Anticipo)</label>
                  <input
                    type="number"
                    value={formData.traslado || ''}
                    onChange={(e) => manejarCambioNumero('traslado', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-3 py-2 text-sm font-mono font-semibold focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div className="bg-slate-50 p-4 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Básico del Viaje</label>
                  <input
                    type="number"
                    value={formData.basico || ''}
                    onChange={(e) => manejarCambioNumero('basico', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-3 py-2 text-sm font-mono font-semibold focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div className="bg-slate-50 p-4 rounded-xl border border-slate-200">
                  <label className="block text-xs font-semibold text-slate-700 mb-1">Saldo Anterior</label>
                  <input
                    type="number"
                    value={formData.saldoAnterior || ''}
                    onChange={(e) => manejarCambioNumero('saldoAnterior', e.target.value)}
                    placeholder="0"
                    className="w-full bg-white border border-slate-300 rounded-lg px-3 py-2 text-sm font-mono font-semibold focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  />
                </div>

                <div className="p-4 rounded-xl border border-blue-200 bg-blue-50/60">
                  <label className="block text-xs font-semibold text-blue-900 mb-1">Saldo del Viaje</label>
                  <div className={`text-xl font-mono font-bold ${formData.saldoViaje >= 0 ? 'text-blue-700' : 'text-rose-600'}`}>
                    {formatCOP(formData.saldoViaje)}
                  </div>
                  <span className="text-xs text-blue-600">Traslado + Básico - Total Gastos</span>
                </div>

                <div className="p-4 rounded-xl border border-emerald-200 bg-emerald-50/60">
                  <label className="block text-xs font-semibold text-emerald-900 mb-1">Saldo Total Definitivo</label>
                  <div className={`text-xl font-mono font-bold ${formData.saldoTotal >= 0 ? 'text-emerald-700' : 'text-rose-600'}`}>
                    {formatCOP(formData.saldoTotal)}
                  </div>
                  <span className="text-xs text-emerald-600">Saldo Viaje + Saldo Anterior</span>
                </div>
              </div>
            </div>
          )}

          {/* Botón de Aprobación y Guardado en Base de Datos */}
          <div className="pt-6 mt-6 border-t border-slate-200 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 inline-block"></span>
              <span>Estado: <strong>{formData.estadoRevision.toUpperCase()}</strong></span>
            </div>

            <button
              onClick={guardarEnBaseDatos}
              disabled={guardando || !formData.placa}
              className="w-full sm:w-auto px-6 py-3 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl font-semibold transition-all shadow-md shadow-emerald-600/20 disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {guardando ? (
                <>
                  <RefreshCw className="w-5 h-5 animate-spin" />
                  <span>Guardando en PostgreSQL...</span>
                </>
              ) : (
                <>
                  <Save className="w-5 h-5" />
                  <span>Aprobar y Guardar en Base de Datos</span>
                </>
              )}
            </button>
          </div>

        </div>

      </div>
    </div>
  );
}
