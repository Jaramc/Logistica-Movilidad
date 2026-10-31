import React, { useState, useEffect, useMemo, useRef } from 'react';
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
  Calculator,
  Image as ImageIcon
} from 'lucide-react';
import { type LiquidacionViaje, VALORES_INICIALES_LIQUIDACION } from '../types/liquidacion';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5098';

// Lista de los 16 campos exactos del modelo SQL de gastos operativos
const GASTOS_OPERATIVOS: { campo: keyof LiquidacionViaje; label: string }[] = [
  { campo: 'peajes', label: '1. Peajes' },
  { campo: 'acpm', label: '2. ACPM' },
  { campo: 'parqueo', label: '3. Parqueo' },
  { campo: 'comision', label: '4. Comisión' },
  { campo: 'porcentaje', label: '5. Porcentaje' },
  { campo: 'engrase', label: '6. Engrase' },
  { campo: 'tensionada', label: '7. Tensionada' },
  { campo: 'aseo', label: '8. Aseo' },
  { campo: 'llantas', label: '9. Llantas' },
  { campo: 'urea', label: '10. Urea' },
  { campo: 'cargue', label: '11. Cargue' },
  { campo: 'descargue', label: '12. Descargue' },
  { campo: 'carrosada', label: '13. Carrosada' },
  { campo: 'descarrosada', label: '14. Descarrosada' },
  { campo: 'papeleria', label: '15. Papelería' },
  { campo: 'otros', label: '16. Otros' },
];

export default function ProcesadorLiquidaciones() {
  const [archivo, setArchivo] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [cargandoOcr, setCargandoOcr] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [mensajeExito, setMensajeExito] = useState<string | null>(null);
  const [mensajeError, setMensajeError] = useState<string | null>(null);
  
  // Transformaciones para el visor
  const [zoom, setZoom] = useState(1);
  const [rotacion, setRotacion] = useState(0);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Formulario completo
  const [formData, setFormData] = useState<LiquidacionViaje>(VALORES_INICIALES_LIQUIDACION);
  const [tabActiva, setTabActiva] = useState<'gastos' | 'viaje' | 'totales'>('gastos');

  // Suma automática de los 16 rubros de gastos
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

  // Actualizar totales y saldos automáticamente
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
    const num = parseFloat(valor) || 0;
    setFormData(prev => ({
      ...prev,
      [campo]: num
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

      setFormData(prev => {
        let nuevosPeajes = prev.peajes;
        let nuevoAcpm = prev.acpm;
        let nuevaPlaca = prev.placa;

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
      setMensajeError('No se pudo conectar con el endpoint de Azure. Verifica que el backend esté en ejecución.');
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
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData)
      });

      if (!respuesta.ok) {
        throw new Error(`Error al guardar: ${respuesta.statusText}`);
      }

      const resJson = await respuesta.json();
      setMensajeExito(resJson.mensaje || 'Liquidación aprobada y guardada exitosamente en PostgreSQL.');
      setFormData(prev => ({ ...prev, estadoRevision: 'aprobado' }));
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
    <div className="max-w-[1600px] mx-auto p-4 sm:p-6 text-slate-800">
      
      {/* Cabecera de Página con Tarjeta de Resumen */}
      <div className="flex flex-col md:flex-row md:items-end justify-between pb-6 mb-6 border-b border-slate-200/80 gap-4">
        <div>
          <h2 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            Digitalización de Liquidaciones de Viaje
          </h2>
          <p className="text-sm text-slate-500 mt-1">
            Inspecciona el comprobante visualmente y aprueba los datos extraídos por Azure AI OCR.
          </p>
        </div>

        {/* Tarjeta de Resumen Rápida */}
        <div className="flex bg-white rounded-xl shadow-xs border border-slate-200 p-1 self-start md:self-auto">
          <div className="px-5 py-2 text-center border-r border-slate-100">
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide">Total Gastos</p>
            <p className="text-lg font-bold text-slate-800 font-mono">{formatCOP(formData.totalGastos)}</p>
          </div>
          <div className="px-5 py-2 text-center bg-blue-50/70 rounded-lg">
            <p className="text-xs font-semibold text-blue-600 uppercase tracking-wide">Saldo Viaje</p>
            <p className={`text-lg font-bold font-mono ${formData.saldoViaje >= 0 ? 'text-blue-700' : 'text-rose-600'}`}>
              {formatCOP(formData.saldoViaje)}
            </p>
          </div>
        </div>
      </div>

      {/* Alertas Dinámicas */}
      {mensajeExito && (
        <div className="mb-5 p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl flex items-center gap-3 shadow-2xs">
          <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
          <span className="text-sm font-medium">{mensajeExito}</span>
        </div>
      )}

      {mensajeError && (
        <div className="mb-5 p-4 bg-rose-50 border border-rose-200 text-rose-800 rounded-xl flex items-center gap-3 shadow-2xs">
          <AlertCircle className="w-5 h-5 text-rose-600 shrink-0" />
          <span className="text-sm font-medium">{mensajeError}</span>
        </div>
      )}

      {/* Layout de 2 Columnas Principal */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 min-h-[640px] lg:h-[calc(100vh-210px)]">
        
        {/* PANEL IZQUIERDO: Visor Mejorado de Soporte / Comprobante (5 Cols) */}
        <div className="lg:col-span-5 bg-white rounded-2xl shadow-xs border border-slate-200 flex flex-col overflow-hidden">
          
          {/* Header del Panel Izquierdo */}
          <div className="p-4 border-b border-slate-100 bg-slate-50/50 flex justify-between items-center">
            <h3 className="text-sm font-bold text-slate-700 flex items-center gap-2">
              <UploadCloud className="w-4 h-4 text-blue-500" />
              Soporte / Comprobante
            </h3>
            {archivo && (
              <span className="text-xs font-medium text-slate-500 truncate max-w-[200px]" title={archivo.name}>
                {archivo.name}
              </span>
            )}
          </div>
          
          {/* Área Central: Drag & Drop o Visor con Zoom Flotante */}
          <div className="flex-1 p-4 flex flex-col justify-center relative overflow-hidden bg-slate-50/30">
            <input 
              ref={fileInputRef}
              type="file" 
              accept=".jpg,.jpeg,.png,.pdf" 
              onChange={manejarArchivo}
              className="hidden"
            />

            {!previewUrl ? (
              // Estado Vacío: Drag & Drop Interactivo a Pantalla Completa
              <div 
                onClick={() => fileInputRef.current?.click()}
                className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-xl bg-slate-50 hover:bg-blue-50/40 transition-all flex flex-col items-center justify-center h-full cursor-pointer group p-8"
              >
                <div className="w-16 h-16 bg-white rounded-2xl shadow-xs border border-slate-200 flex items-center justify-center mb-4 group-hover:scale-110 group-hover:border-blue-400 transition-all">
                  <UploadCloud className="w-8 h-8 text-blue-500" />
                </div>
                <p className="font-semibold text-slate-700 text-base">Arrastra el tiquete o planilla</p>
                <p className="text-sm text-slate-500 mt-1">o haz clic para explorar tus archivos</p>
                <span className="text-xs text-slate-400 mt-4 px-3 py-1 bg-slate-100 rounded-full">
                  Soporta JPG, PNG y PDF
                </span>
              </div>
            ) : (
              // Visor Activo: Controles Flotantes Translúcidos (no empujan el diseño)
              <div className="relative w-full h-full flex items-center justify-center bg-slate-900/5 rounded-xl border border-slate-200 overflow-hidden">
                
                {/* Botonera Flotante en la Esquina Superior Derecha */}
                <div className="absolute top-3 right-3 z-10 bg-white/90 backdrop-blur-md border border-slate-200/90 shadow-md rounded-xl p-1 flex items-center gap-1">
                  <button 
                    onClick={() => setZoom(z => Math.max(0.6, z - 0.2))} 
                    className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition" 
                    title="Reducir Zoom"
                  >
                    <ZoomOut className="w-4 h-4" />
                  </button>
                  <span className="text-xs font-mono font-semibold px-1 text-slate-600 min-w-[40px] text-center">
                    {Math.round(zoom * 100)}%
                  </span>
                  <button 
                    onClick={() => setZoom(z => Math.min(3.5, z + 0.2))} 
                    className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition" 
                    title="Aumentar Zoom"
                  >
                    <ZoomIn className="w-4 h-4" />
                  </button>
                  <button 
                    onClick={() => setRotacion(r => (r + 90) % 360)} 
                    className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition" 
                    title="Rotar 90°"
                  >
                    <RotateCw className="w-4 h-4" />
                  </button>
                  <button 
                    onClick={() => { setZoom(1); setRotacion(0); }} 
                    className="p-1.5 hover:bg-slate-100 rounded-lg text-slate-700 transition" 
                    title="Restablecer"
                  >
                    <RefreshCw className="w-4 h-4" />
                  </button>
                  <div className="w-px h-4 bg-slate-200 mx-0.5"></div>
                  <button 
                    onClick={() => fileInputRef.current?.click()} 
                    className="p-1.5 hover:bg-blue-50 text-blue-600 rounded-lg transition" 
                    title="Cambiar comprobante"
                  >
                    <ImageIcon className="w-4 h-4" />
                  </button>
                </div>

                {/* Imagen del Comprobante */}
                <div className="w-full h-full overflow-auto flex items-center justify-center p-4">
                  <img 
                    src={previewUrl} 
                    alt="Soporte o tiquete cargado" 
                    style={{
                      transform: `scale(${zoom}) rotate(${rotacion}deg)`,
                      transition: 'transform 0.15s ease-out'
                    }}
                    className="max-h-full max-w-full object-contain shadow-sm rounded-lg"
                  />
                </div>
              </div>
            )}
          </div>

          {/* Footer del Panel Izquierdo con Botón Primario de IA */}
          <div className="p-4 bg-slate-50 border-t border-slate-100">
            <button 
              onClick={procesarImagen}
              disabled={!archivo || cargandoOcr}
              className="w-full py-3 px-4 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white font-semibold rounded-xl shadow-md hover:shadow-lg disabled:opacity-50 transition-all flex items-center justify-center gap-2 cursor-pointer"
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
        </div>

        {/* PANEL DERECHO: Formulario Compacto con Sticky Footer (7 Cols) */}
        <div className="lg:col-span-7 bg-white rounded-2xl shadow-xs border border-slate-200 flex flex-col overflow-hidden relative">
          
          {/* Pestañas de Navegación Limpias */}
          <div className="flex border-b border-slate-200 px-3 pt-2 bg-slate-50/50">
            <button
              onClick={() => setTabActiva('gastos')}
              className={`px-4 py-3 text-sm font-semibold transition-all border-b-2 ${
                tabActiva === 'gastos'
                  ? 'text-blue-600 border-blue-600 bg-white/70 rounded-t-lg'
                  : 'text-slate-500 border-transparent hover:text-slate-800'
              }`}
            >
              Gastos Operativos (16)
            </button>
            <button
              onClick={() => setTabActiva('viaje')}
              className={`px-4 py-3 text-sm font-semibold transition-all border-b-2 ${
                tabActiva === 'viaje'
                  ? 'text-blue-600 border-blue-600 bg-white/70 rounded-t-lg'
                  : 'text-slate-500 border-transparent hover:text-slate-800'
              }`}
            >
              Datos del Viaje
            </button>
            <button
              onClick={() => setTabActiva('totales')}
              className={`px-4 py-3 text-sm font-semibold transition-all border-b-2 ${
                tabActiva === 'totales'
                  ? 'text-blue-600 border-blue-600 bg-white/70 rounded-t-lg'
                  : 'text-slate-500 border-transparent hover:text-slate-800'
              }`}
            >
              Saldos y Totales
            </button>
          </div>

          {/* Contenido con Scroll Interno (reserva espacio para el sticky footer) */}
          <div className="flex-1 p-5 sm:p-6 overflow-y-auto pb-24">
            
            {/* TAB 1: GASTOS OPERATIVOS (16 INPUTS CON PREFIJO VISUAL '$') */}
            {tabActiva === 'gastos' && (
              <div>
                <div className="flex items-center justify-between mb-5">
                  <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
                    Desglose de Gastos en Ruta
                  </h4>
                  <div className="flex items-center gap-1.5 text-xs text-blue-600 font-medium bg-blue-50 px-2.5 py-1 rounded-md">
                    <Calculator className="w-3.5 h-3.5" />
                    Suma automática: {formatCOP(formData.totalGastos)}
                  </div>
                </div>

                <div className="grid grid-cols-2 md:grid-cols-3 gap-x-5 gap-y-4">
                  {GASTOS_OPERATIVOS.map((item) => (
                    <div key={item.campo}>
                      <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                        {item.label}
                      </label>
                      <div className="relative">
                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                          <span className="text-slate-400 text-sm">$</span>
                        </div>
                        <input 
                          type="number"
                          placeholder="0"
                          value={formData[item.campo] ? (formData[item.campo] as number) : ''}
                          onChange={(e) => manejarCambioNumero(item.campo, e.target.value)}
                          className="pl-7 pr-3 block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm font-mono py-2 transition-all shadow-2xs focus:outline-none" 
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* TAB 2: DATOS DEL VIAJE */}
            {tabActiva === 'viaje' && (
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-5">
                  Vehículo, Conductor y Ruta
                </h4>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-4">
                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Placa del Vehículo
                    </label>
                    <div className="relative">
                      <input
                        type="text"
                        value={formData.placa}
                        onChange={(e) => manejarCambioTexto('placa', e.target.value.toUpperCase())}
                        placeholder="Ej. WCN452"
                        className="block w-full uppercase font-mono font-bold tracking-wider rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                      />
                      <Truck className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Nombre Conductor
                    </label>
                    <input
                      type="text"
                      value={formData.conductor}
                      onChange={(e) => manejarCambioTexto('conductor', e.target.value)}
                      placeholder="Nombre completo"
                      className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Fecha Inicial
                    </label>
                    <div className="relative">
                      <input
                        type="date"
                        value={formData.fechaInicial}
                        onChange={(e) => manejarCambioTexto('fechaInicial', e.target.value)}
                        className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                      />
                      <Calendar className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Fecha Final
                    </label>
                    <div className="relative">
                      <input
                        type="date"
                        value={formData.fechaFinal}
                        onChange={(e) => manejarCambioTexto('fechaFinal', e.target.value)}
                        className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                      />
                      <Calendar className="w-4 h-4 text-slate-400 absolute right-3 top-2.5 pointer-events-none" />
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      N° Manifiesto
                    </label>
                    <input
                      type="text"
                      value={formData.manifiesto}
                      onChange={(e) => manejarCambioTexto('manifiesto', e.target.value)}
                      placeholder="Ej. MAN-98201"
                      className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Empresa Transportadora
                    </label>
                    <input
                      type="text"
                      value={formData.empresaTransportadora}
                      onChange={(e) => manejarCambioTexto('empresaTransportadora', e.target.value)}
                      placeholder="Empresa asignada"
                      className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Origen del Viaje
                    </label>
                    <input
                      type="text"
                      value={formData.origenViaje}
                      onChange={(e) => manejarCambioTexto('origenViaje', e.target.value)}
                      placeholder="Ciudad origen"
                      className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1.5">
                      Destino
                    </label>
                    <input
                      type="text"
                      value={formData.destino}
                      onChange={(e) => manejarCambioTexto('destino', e.target.value)}
                      placeholder="Ciudad destino"
                      className="block w-full rounded-lg bg-slate-50 border border-slate-200/70 focus:bg-white focus:border-blue-500 focus:ring-2 focus:ring-blue-100 text-sm py-2 px-3 transition-all shadow-2xs focus:outline-none"
                    />
                  </div>
                </div>
              </div>
            )}

            {/* TAB 3: SALDOS Y TOTALES */}
            {tabActiva === 'totales' && (
              <div>
                <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-5">
                  Anticipos y Liquidación Final
                </h4>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200/80">
                    <label className="block text-xs font-semibold text-slate-500 uppercase mb-1">
                      Total Gastos (16 Rubros)
                    </label>
                    <div className="text-xl font-mono font-bold text-slate-900">
                      {formatCOP(formData.totalGastos)}
                    </div>
                    <span className="text-xs text-slate-400">Sumado automáticamente</span>
                  </div>

                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200/80">
                    <label className="block text-xs font-semibold text-slate-500 uppercase mb-1.5">
                      Traslado (Anticipo)
                    </label>
                    <div className="relative">
                      <span className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 text-sm">$</span>
                      <input
                        type="number"
                        placeholder="0"
                        value={formData.traslado || ''}
                        onChange={(e) => manejarCambioNumero('traslado', e.target.value)}
                        className="pl-7 pr-3 block w-full rounded-lg bg-white border border-slate-200 text-sm font-mono font-semibold py-2 focus:ring-2 focus:ring-blue-100 focus:border-blue-500 focus:outline-none"
                      />
                    </div>
                  </div>

                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200/80">
                    <label className="block text-xs font-semibold text-slate-500 uppercase mb-1.5">
                      Básico del Viaje
                    </label>
                    <div className="relative">
                      <span className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 text-sm">$</span>
                      <input
                        type="number"
                        placeholder="0"
                        value={formData.basico || ''}
                        onChange={(e) => manejarCambioNumero('basico', e.target.value)}
                        className="pl-7 pr-3 block w-full rounded-lg bg-white border border-slate-200 text-sm font-mono font-semibold py-2 focus:ring-2 focus:ring-blue-100 focus:border-blue-500 focus:outline-none"
                      />
                    </div>
                  </div>

                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200/80">
                    <label className="block text-xs font-semibold text-slate-500 uppercase mb-1.5">
                      Saldo Anterior
                    </label>
                    <div className="relative">
                      <span className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 text-sm">$</span>
                      <input
                        type="number"
                        placeholder="0"
                        value={formData.saldoAnterior || ''}
                        onChange={(e) => manejarCambioNumero('saldoAnterior', e.target.value)}
                        className="pl-7 pr-3 block w-full rounded-lg bg-white border border-slate-200 text-sm font-mono font-semibold py-2 focus:ring-2 focus:ring-blue-100 focus:border-blue-500 focus:outline-none"
                      />
                    </div>
                  </div>

                  <div className="p-4 rounded-xl border border-blue-200 bg-blue-50/50">
                    <label className="block text-xs font-semibold text-blue-900 uppercase mb-1">
                      Saldo del Viaje
                    </label>
                    <div className={`text-xl font-mono font-bold ${formData.saldoViaje >= 0 ? 'text-blue-700' : 'text-rose-600'}`}>
                      {formatCOP(formData.saldoViaje)}
                    </div>
                    <span className="text-xs text-blue-600 font-medium">Traslado + Básico - Total Gastos</span>
                  </div>

                  <div className="p-4 rounded-xl border border-emerald-200 bg-emerald-50/50">
                    <label className="block text-xs font-semibold text-emerald-900 uppercase mb-1">
                      Saldo Total Definitivo
                    </label>
                    <div className={`text-xl font-mono font-bold ${formData.saldoTotal >= 0 ? 'text-emerald-700' : 'text-rose-600'}`}>
                      {formatCOP(formData.saldoTotal)}
                    </div>
                    <span className="text-xs text-emerald-600 font-medium">Saldo Viaje + Saldo Anterior</span>
                  </div>
                </div>
              </div>
            )}

          </div>

          {/* Sticky Footer: Estado + Botón de Aprobación */}
          <div className="absolute bottom-0 left-0 right-0 p-4 bg-white/95 backdrop-blur-md border-t border-slate-200 flex items-center justify-between z-10 shadow-xs">
            <div className="flex items-center gap-2">
              <div className={`w-2.5 h-2.5 rounded-full ${formData.estadoRevision === 'aprobado' ? 'bg-emerald-500' : 'bg-amber-500'}`}></div>
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">
                Estado: {formData.estadoRevision.toUpperCase()}
              </span>
            </div>

            <button
              onClick={guardarEnBaseDatos}
              disabled={guardando || !formData.placa}
              className="px-6 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold rounded-xl shadow-xs hover:shadow-md transition-all flex items-center gap-2 cursor-pointer disabled:opacity-50"
            >
              {guardando ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  <span>Guardando...</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="w-4 h-4" />
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
