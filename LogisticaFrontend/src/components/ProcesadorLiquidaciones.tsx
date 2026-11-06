import { useState, useEffect, useRef, useMemo } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { 
  PlanillaFormSchema, 
  type PlanillaFormData, 
  type GuardarPlanillaDto 
} from '../features/planillas/schemas/planillaSchema';
import { calcularLiquidacionEconomica, toApiDecimalNumber } from '../features/planillas/utils/money';
import { usePlanillaExtraccion } from '../features/planillas/hooks/usePlanillaExtraccion';
import { useGuardarPlanilla } from '../features/planillas/hooks/useGuardarPlanilla';
import { UploadDropzone } from '../features/planillas/components/UploadDropzone';
import { VisorDocumento } from '../features/planillas/components/VisorDocumento';
import { ResumenFinanciero } from '../features/planillas/components/ResumenFinanciero';
import { DatosViajeForm } from '../features/planillas/components/DatosViajeForm';
import { GastosTable } from '../features/planillas/components/GastosTable';
import { Alert } from './ui/Alert';
import { Button } from './ui/Button';
import { Sparkles, CheckCircle2, RotateCcw } from 'lucide-react';

export type EstadoFlujo = 'idle' | 'archivoSeleccionado' | 'extrayendo' | 'revisando' | 'guardado';

const FECHA_HOY = new Date().toISOString().split('T')[0] ?? '';
const ARRAY_VACIO_GASTOS: any[] = [];

export default function ProcesadorLiquidaciones() {
  const [estado, setEstado] = useState<EstadoFlujo>('idle');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [tabActiva, setTabActiva] = useState<'gastos' | 'viaje'>('gastos');

  const reviewHeadingRef = useRef<HTMLHeadingElement>(null);

  // Inicialización de React Hook Form con Zod Resolver
  const form = useForm<PlanillaFormData>({
    resolver: zodResolver(PlanillaFormSchema),
    defaultValues: {
      placa: '',
      conductor: '',
      origen: '',
      destino: '',
      fechaInicial: FECHA_HOY,
      fechaFinal: FECHA_HOY,
      manifiesto: '',
      empresaTransportadora: '',
      flete: 0,
      anticipo: 0,
      saldoAnterior: 0,
      detalleGastos: [],
    },
    mode: 'onTouched',
  });

  const { reset: resetForm, handleSubmit, formState: { isDirty } } = form;

  // Hooks de mutación con TanStack Query
  const { extraer, estaExtrayendo, error: errorOcr, reset: resetOcr } = usePlanillaExtraccion();
  const { guardar, estaGuardando, error: errorGuardado, esExitoso: guardadoExitoso, reset: resetGuardar } = useGuardarPlanilla();

  // Suscripción reactiva para el cálculo puro de saldos y totales
  const formValores = useWatch({ control: form.control });
  const fleteVal = formValores?.flete ?? 0;
  const anticipoVal = formValores?.anticipo ?? 0;
  const gastosVal = formValores?.detalleGastos ?? ARRAY_VACIO_GASTOS;
  const saldoAnteriorVal = formValores?.saldoAnterior ?? 0;

  const calculo = useMemo(() => {
    return calcularLiquidacionEconomica(
      fleteVal,
      anticipoVal,
      gastosVal,
      saldoAnteriorVal
    );
  }, [fleteVal, anticipoVal, gastosVal, saldoAnteriorVal]);

  // Protección against datos sin guardar (beforeunload)
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (isDirty && estado !== 'guardado') {
        e.preventDefault();
      }
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty, estado]);

  // Procesar archivo seleccionado
  const handleArchivoAceptado = async (file: File) => {
    setArchivo(file);
    setEstado('extrayendo');
    resetOcr();
    resetGuardar();

    try {
      const respuesta = await extraer(file);
      const data = respuesta.data;

      if (data) {
        resetForm({
          placa: data.placa || '',
          conductor: form.getValues('conductor') || '',
          origen: data.origen || '',
          destino: data.destino || '',
          fechaInicial: form.getValues('fechaInicial'),
          fechaFinal: form.getValues('fechaFinal'),
          manifiesto: '',
          empresaTransportadora: '',
          flete: data.flete || 0,
          anticipo: data.anticipo || 0,
          saldoAnterior: 0,
          detalleGastos: (data.detalleGastos || []).map((g, idx) => ({
            id: `ocr_${idx}_${Date.now()}`,
            concepto: g.concepto || `Gasto #${idx + 1}`,
            valor: g.valor || 0,
          })),
        });
      }

      setEstado('revisando');
      setTimeout(() => reviewHeadingRef.current?.focus(), 100);
    } catch {
      setEstado('archivoSeleccionado');
    }
  };

  // Guardar en Base de Datos (Mapeo estricto con DTO Whitelist)
  const onGuardarAprobado = async (data: PlanillaFormData) => {
    const payload: GuardarPlanillaDto = {
      placa: data.placa,
      conductor: data.conductor,
      origen: data.origen,
      destino: data.destino,
      fechaInicial: data.fechaInicial,
      fechaFinal: data.fechaFinal,
      manifiesto: data.manifiesto,
      empresaTransportadora: data.empresaTransportadora,
      flete: toApiDecimalNumber(data.flete),
      anticipo: toApiDecimalNumber(data.anticipo),
      totalGastos: toApiDecimalNumber(calculo.totalGastos),
      saldoViaje: toApiDecimalNumber(calculo.saldoViaje),
      saldoAnterior: toApiDecimalNumber(data.saldoAnterior),
      saldoTotal: toApiDecimalNumber(calculo.saldoTotal),
      gastos: (data.detalleGastos || []).map((g) => ({
        concepto: g.concepto,
        valor: toApiDecimalNumber(g.valor),
      })),
    };

    try {
      await guardar(payload);
      setEstado('guardado');
    } catch {
      // Error manejado en errorGuardado
    }
  };

  const reiniciarProceso = () => {
    setArchivo(null);
    setEstado('idle');
    resetForm();
    resetOcr();
    resetGuardar();
  };

  return (
    <div className="max-w-[1600px] mx-auto p-4 sm:p-6 text-slate-800">
      {/* Header Principal con Resumen Financiero Fijo */}
      <div className="flex flex-col gap-4 pb-6 mb-6 border-b border-slate-200">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h2
              ref={reviewHeadingRef}
              tabIndex={-1}
              className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight outline-none"
            >
              Digitalización de Planillas de Gastos
            </h2>
            <p className="text-sm text-slate-500 mt-1">
              Extracción inteligente con Azure AI Document Intelligence y validación contable asistida.
            </p>
          </div>

          {estado !== 'idle' && (
            <Button
              variant="outline"
              size="sm"
              onClick={reiniciarProceso}
              leftIcon={<RotateCcw className="w-3.5 h-3.5" />}
            >
              Nueva Planilla
            </Button>
          )}
        </div>

        {/* Resumen Financiero (Visible siempre que haya datos o archivo) */}
        {(estado === 'revisando' || estado === 'guardado' || gastosVal.length > 0) && (
          <ResumenFinanciero
            flete={fleteVal}
            anticipo={anticipoVal}
            calculo={calculo}
            cantidadGastos={gastosVal.length}
          />
        )}
      </div>

      {/* Alertas de Estado del Servidor */}
      {guardadoExitoso && estado === 'guardado' && (
        <Alert variant="success" title="Liquidación Guardada y Aprobada" className="mb-6">
          La planilla de viaje fue validada y persistida exitosamente en PostgreSQL.
        </Alert>
      )}

      {errorOcr && (
        <Alert
          variant="error"
          title="Fallo en la Extracción con IA"
          correlationId={errorOcr.correlationId}
          onDismiss={resetOcr}
          className="mb-6"
        >
          {errorOcr.getMensajeUsuario()}
        </Alert>
      )}

      {errorGuardado && (
        <Alert
          variant="error"
          title="Error al Guardar la Liquidación"
          correlationId={errorGuardado.correlationId}
          onDismiss={resetGuardar}
          className="mb-6"
        >
          {errorGuardado.getMensajeUsuario()}
        </Alert>
      )}

      {/* Flujo: Dropzone Inicial si no hay archivo */}
      {estado === 'idle' && (
        <div className="max-w-2xl mx-auto my-12">
          <UploadDropzone onArchivoAceptado={handleArchivoAceptado} />
        </div>
      )}

      {/* Flujo: Estado de Carga con Skeleton */}
      {estaExtrayendo && (
        <div className="py-20 text-center flex flex-col items-center justify-center bg-white rounded-2xl border border-slate-200">
          <div className="w-16 h-16 rounded-2xl bg-blue-50 border border-blue-200 flex items-center justify-center mb-4">
            <Sparkles className="w-8 h-8 text-blue-600 animate-pulse" />
          </div>
          <h3 className="text-lg font-bold text-slate-800 mb-1">
            Analizando planilla con Azure Document Intelligence...
          </h3>
          <p className="text-sm text-slate-500 max-w-md">
            Identificando placa, flete, anticipo y renglones dinámicos de gastos del comprobante.
          </p>
        </div>
      )}

      {/* Flujo: Pantalla de Revisión Dividida (Split Screen en Desktop) */}
      {(estado === 'revisando' || estado === 'guardado') && !estaExtrayendo && (
        <form onSubmit={handleSubmit(onGuardarAprobado)} className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Panel Izquierdo: Visor de Documento Original (5 Cols) */}
          <div className="lg:col-span-5 h-[550px] lg:h-[720px] sticky top-20">
            <VisorDocumento archivo={archivo} onCambiarArchivo={reiniciarProceso} />
          </div>

          {/* Panel Derecho: Formulario Estructurado (7 Cols) */}
          <div className="lg:col-span-7 bg-white rounded-2xl border border-slate-200 shadow-2xs flex flex-col overflow-hidden">
            {/* Pestañas de Edición */}
            <div className="flex border-b border-slate-200 bg-slate-50/50 px-4 pt-2 gap-2">
              <button
                type="button"
                onClick={() => setTabActiva('gastos')}
                className={`px-4 py-3 text-sm font-semibold border-b-2 transition-colors ${
                  tabActiva === 'gastos'
                    ? 'border-blue-600 text-blue-600 bg-white rounded-t-xl'
                    : 'border-transparent text-slate-500 hover:text-slate-800'
                }`}
              >
                Renglones de Gastos ({gastosVal.length})
              </button>
              <button
                type="button"
                onClick={() => setTabActiva('viaje')}
                className={`px-4 py-3 text-sm font-semibold border-b-2 transition-colors ${
                  tabActiva === 'viaje'
                    ? 'border-blue-600 text-blue-600 bg-white rounded-t-xl'
                    : 'border-transparent text-slate-500 hover:text-slate-800'
                }`}
              >
                Datos del Viaje y Contrato
              </button>
            </div>

            <div className="p-5 sm:p-6 flex-1 overflow-y-auto">
              {tabActiva === 'gastos' && <GastosTable control={form.control} />}
              {tabActiva === 'viaje' && <DatosViajeForm form={form} />}
            </div>

            {/* Footer de Acciones y Guardado Seguro */}
            <div className="p-4 bg-slate-50 border-t border-slate-200 flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-500 flex items-center gap-1.5">
                <span className="w-2 h-2 rounded-full bg-emerald-500" />
                {estado === 'guardado' ? 'Aprobado y Registrado' : 'Pendiente de Aprobación'}
              </span>

              <Button
                type="submit"
                variant="success"
                isLoading={estaGuardando}
                loadingText="Guardando en base de datos..."
                disabled={estado === 'guardado'}
                leftIcon={<CheckCircle2 className="w-4 h-4" />}
              >
                {estado === 'guardado' ? 'Planilla Aprobada' : 'Aprobar y Guardar en BD'}
              </Button>
            </div>
          </div>
        </form>
      )}
    </div>
  );
}
