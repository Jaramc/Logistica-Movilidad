import { useState, useEffect, useRef, useMemo } from 'react';
import { useForm, useWatch, type FieldErrors } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  PlanillaFormSchema,
  type PlanillaFormData,
  type GuardarPlanillaDto,
  type SaldoDeclarado,
  conceptoDeGasto,
  normalizarPlaca
} from '../features/planillas/schemas/planillaSchema';
import { calcularLiquidacionEconomica, describirSaldo, formatCOP, toApiDecimalNumber } from '../features/planillas/utils/money';
import { SaldosDeclarados as BloqueSaldosDeclarados } from '../features/planillas/components/SaldosDeclarados';
import Big from 'big.js';
import { useSaldoAnterior } from '../features/planillas/hooks/useSaldoAnterior';
import { useConductores } from '../features/planillas/hooks/useConductores';
import { resolverConductor } from '../features/planillas/utils/conductor';
import { resumirErrores } from '../features/planillas/utils/erroresFormulario';
import { hoyLocalISO } from '../lib/fechas';
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

const ARRAY_VACIO_GASTOS: any[] = [];

/** Lo escrito en papel en la sección SALDOS (no lo calculado). */
export interface SaldosDeclarados {
  totalGastos: number | null;
  saldoViaje: SaldoDeclarado | null;
  saldoAnterior: SaldoDeclarado | null;
  saldoNuevo: SaldoDeclarado | null;
}

export default function ProcesadorLiquidaciones() {
  const [estado, setEstado] = useState<EstadoFlujo>('idle');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [tabActiva, setTabActiva] = useState<'gastos' | 'viaje'>('gastos');
  // Lo que devolvió el OCR y no va en el formulario: se reenvía (jsonOcrCrudo) o se muestra al revisor.
  const [jsonOcrCrudo, setJsonOcrCrudo] = useState<string | null>(null);
  const [camposNoEncontrados, setCamposNoEncontrados] = useState<string[]>([]);
  const [confianzaPromedio, setConfianzaPromedio] = useState<number | null>(null);
  const [declarados, setDeclarados] = useState<SaldosDeclarados | null>(null);
  const [advertenciasOcr, setAdvertenciasOcr] = useState<string[]>([]);
  const [revisoDiferencias, setRevisoDiferencias] = useState(false);
  // Primera planilla del conductor en el sistema: usar el saldo anterior escrito como saldo inicial.
  const [usarSaldoInicial, setUsarSaldoInicial] = useState(true);
  // Campos con error al intentar aprobar (pueden estar en la otra pestaña).
  const [camposConError, setCamposConError] = useState<string[]>([]);
  const conductoresConocidos = useConductores();

  const reviewHeadingRef = useRef<HTMLHeadingElement>(null);

  // Inicialización de React Hook Form con Zod Resolver
  const form = useForm<PlanillaFormData>({
    resolver: zodResolver(PlanillaFormSchema),
    defaultValues: {
      placa: '',
      conductor: '',
      origen: '',
      destino: '',
      fechaInicial: hoyLocalISO(),
      fechaFinal: hoyLocalISO(),
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
  const { guardar, estaGuardando, error: errorGuardado, esExitoso: guardadoExitoso, resultado: guardado, reset: resetGuardar } = useGuardarPlanilla();

  // Suscripción reactiva para el cálculo puro de saldos y totales
  const formValores = useWatch({ control: form.control });
  const fleteVal = formValores?.flete ?? 0;
  const anticipoVal = formValores?.anticipo ?? 0;
  const gastosVal = formValores?.detalleGastos ?? ARRAY_VACIO_GASTOS;
  const saldoAnteriorVal = formValores?.saldoAnterior ?? 0;

  // El saldo anterior lo asigna el servidor (SaldoTotal de la última planilla del conductor): se
  // consulta para mostrar el cálculo real antes de aprobar y compararlo con el escrito en papel.
  const {
    saldoAnterior: saldoAnteriorServidor,
    planillaAnteriorId,
    consultado: saldoConsultado,
  } = useSaldoAnterior(formValores?.conductor);
  const saldoAnteriorDeclarado = declarados?.saldoAnterior ?? null;
  // Sin planillas previas del conductor el sistema arrancaría en 0: se ofrece tomar el saldo escrito.
  const esPrimeraPlanilla = saldoConsultado && planillaAnteriorId === null && saldoAnteriorDeclarado !== null;
  const aplicaSaldoInicial = esPrimeraPlanilla && usarSaldoInicial;
  const saldoAnteriorEfectivo = aplicaSaldoInicial ? saldoAnteriorDeclarado!.valor : saldoAnteriorServidor;
  useEffect(() => {
    if (estado === 'guardado') return;
    form.setValue('saldoAnterior', saldoAnteriorEfectivo ?? 0, { shouldDirty: false });
  }, [saldoAnteriorEfectivo, estado, form]);
  const difiereSaldoAnterior =
    !esPrimeraPlanilla &&
    saldoAnteriorServidor !== null && saldoAnteriorDeclarado !== null && saldoAnteriorServidor !== saldoAnteriorDeclarado.valor;

  const calculo = useMemo(() => {
    return calcularLiquidacionEconomica(
      fleteVal,
      anticipoVal,
      gastosVal,
      saldoAnteriorVal
    );
  }, [fleteVal, anticipoVal, gastosVal, saldoAnteriorVal]);

  // Diferencia viva entre la suma de los rubros y el total escrito en papel: exige confirmación.
  const diferenciaTotal =
    declarados?.totalGastos != null ? new Big(declarados.totalGastos).minus(calculo.totalGastos) : new Big(0);
  const hayDiferenciaTotal = !diferenciaTotal.eq(0);
  const bloqueadoPorDiferencias = hayDiferenciaTotal && !revisoDiferencias;

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
    limpiarDatosOcr();

    try {
      const respuesta = await extraer(file);
      const data = respuesta.data;

      if (data) {
        setJsonOcrCrudo(data.jsonOcrCrudo);
        setCamposNoEncontrados(data.camposNoEncontrados);
        setConfianzaPromedio(data.confianzaPromedio);
        setAdvertenciasOcr(data.advertencias);
        setDeclarados({
          totalGastos: data.totalGastosDeclarado,
          saldoViaje: data.saldoViajeDeclarado,
          saldoAnterior: data.saldoAnteriorDeclarado,
          saldoNuevo: data.saldoNuevoDeclarado,
        });

        resetForm({
          placa: data.placa || '',
          // El OCR solo trae el nombre escrito en los saldos ("Pedro"): se completa con el conductor conocido.
          conductor: data.conductor
            ? resolverConductor(data.conductor, conductoresConocidos)
            : form.getValues('conductor') || '',
          origen: data.origen || '',
          destino: data.destino || '',
          fechaInicial: data.fechaInicial ?? form.getValues('fechaInicial'),
          fechaFinal: data.fechaFinal ?? form.getValues('fechaFinal'),
          manifiesto: data.manifiesto,
          empresaTransportadora: data.empresaTransportadora,
          flete: data.flete || 0,
          anticipo: data.anticipo || 0,
          saldoAnterior: 0,
          detalleGastos: (data.detalleGastos || []).map((g, idx) => ({
            id: `ocr_${idx}_${Date.now()}`,
            // Filas del OCR sin etiqueta impresa: el texto leído queda como descripción editable.
            etiqueta: g.etiqueta,
            descripcion: g.descripcion || (g.etiqueta ? '' : g.concepto),
            categoria: g.categoriaPropuesta,
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
      placa: normalizarPlaca(data.placa),
      conductor: data.conductor,
      origen: data.origen,
      destino: data.destino,
      fechaInicial: data.fechaInicial,
      fechaFinal: data.fechaFinal,
      manifiesto: data.manifiesto,
      empresaTransportadora: data.empresaTransportadora,
      flete: toApiDecimalNumber(data.flete),
      anticipo: toApiDecimalNumber(data.anticipo),
      gastos: (data.detalleGastos || []).map((g) => ({
        concepto: conceptoDeGasto(g),
        valor: toApiDecimalNumber(g.valor),
        etiqueta: g.etiqueta.trim() || null,
        descripcion: g.descripcion.trim() || null,
        categoria: g.categoria,
      })),
      jsonOcrCrudo,
      totalGastosDeclarado: declarados?.totalGastos ?? null,
      saldoViajeDeclarado: declarados?.saldoViaje?.valor ?? null,
      saldoAnteriorDeclarado: declarados?.saldoAnterior?.valor ?? null,
      saldoNuevoDeclarado: declarados?.saldoNuevo?.valor ?? null,
      usarSaldoAnteriorDeclarado: aplicaSaldoInicial,
    };
    setCamposConError([]);

    try {
      await guardar(payload);
      setEstado('guardado');
    } catch {
      // Error manejado en errorGuardado
    }
  };

  // Si falta un dato en la pestaña que no se ve, el botón "parecía no hacer nada": se cambia de
  // pestaña y se listan los campos pendientes.
  const onFormularioInvalido = (errores: FieldErrors<PlanillaFormData>) => {
    const { pestana, campos } = resumirErrores(errores);
    setCamposConError(campos);
    setTabActiva(pestana);
  };

  const limpiarDatosOcr = () => {
    setJsonOcrCrudo(null);
    setCamposNoEncontrados([]);
    setConfianzaPromedio(null);
    setDeclarados(null);
    setAdvertenciasOcr([]);
    setRevisoDiferencias(false);
    setUsarSaldoInicial(true);
    setCamposConError([]);
  };

  const reiniciarProceso = () => {
    limpiarDatosOcr();
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
            saldoAnterior={saldoAnteriorVal}
          />
        )}

        {declarados && (estado === 'revisando' || estado === 'guardado') && (
          <BloqueSaldosDeclarados
            totalGastos={declarados.totalGastos}
            saldoViaje={declarados.saldoViaje}
            saldoAnterior={declarados.saldoAnterior}
            saldoNuevo={declarados.saldoNuevo}
            calculo={calculo}
            saldoAnteriorServidor={saldoAnteriorEfectivo}
          />
        )}
      </div>

      {/* Alertas de Estado del Servidor */}
      {guardadoExitoso && estado === 'guardado' && (
        <Alert variant="success" title="Liquidación Guardada y Aprobada" className="mb-6">
          La planilla de viaje fue validada y persistida exitosamente en PostgreSQL.
        </Alert>
      )}

      {guardadoExitoso && estado === 'guardado' && (guardado?.advertencias?.length ?? 0) > 0 && (
        <Alert variant="warning" title="Revisa estas diferencias" className="mb-6">
          <ul className="list-disc pl-4 space-y-1">
            {guardado!.advertencias!.map((a) => <li key={a}>{a}</li>)}
          </ul>
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
        <form onSubmit={handleSubmit(onGuardarAprobado, onFormularioInvalido)} className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Panel Izquierdo: Visor de Documento Original (5 Cols) */}
          <div className="lg:col-span-5 h-[550px] lg:h-[720px] sticky top-20">
            <VisorDocumento archivo={archivo} onCambiarArchivo={reiniciarProceso} />
          </div>

          {/* Panel Derecho: Formulario Estructurado (7 Cols) */}
          <div className="lg:col-span-7 bg-white rounded-2xl border border-slate-200 shadow-2xs flex flex-col overflow-hidden">
            {(confianzaPromedio !== null || camposNoEncontrados.length > 0) && (
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 px-5 py-2 text-xs border-b border-slate-200 bg-white">
                {confianzaPromedio !== null && (
                  <span className="font-semibold text-slate-600" data-testid="confianza-ocr">
                    Confianza OCR: {Math.round(confianzaPromedio * 100)}%
                  </span>
                )}
                {camposNoEncontrados.length > 0 && (
                  <span className="text-amber-700">
                    No encontrados: {camposNoEncontrados.join(', ')}. Revísalos antes de aprobar.
                  </span>
                )}
              </div>
            )}

            {difiereSaldoAnterior && estado !== 'guardado' && (
              <div className="px-5 py-2 text-xs border-b border-amber-200 bg-amber-50 text-amber-900" role="status" data-testid="aviso-saldo-anterior">
                El saldo anterior escrito en la planilla ({describirSaldo(saldoAnteriorDeclarado!.valor)}) difiere del que
                asigna el sistema ({describirSaldo(saldoAnteriorServidor!)}). Se guardará el del sistema.
              </div>
            )}

            {esPrimeraPlanilla && estado !== 'guardado' && (
              <label
                className="flex items-start gap-2 px-5 py-2 text-xs border-b border-blue-200 bg-blue-50 text-blue-900 cursor-pointer"
                data-testid="opcion-saldo-inicial"
              >
                <input
                  type="checkbox"
                  checked={usarSaldoInicial}
                  onChange={(e) => setUsarSaldoInicial(e.target.checked)}
                  className="mt-0.5"
                />
                <span>
                  <strong>Primera planilla de este conductor en el sistema.</strong> Usar el saldo anterior escrito en
                  papel ({describirSaldo(saldoAnteriorDeclarado!.valor)}) como saldo inicial. Si lo desmarcas, su cuenta
                  arranca en $0.
                </span>
              </label>
            )}

            {camposConError.length > 0 && estado !== 'guardado' && (
              <div
                className="px-5 py-2 text-xs border-b border-red-200 bg-red-50 text-red-800"
                role="alert"
                data-testid="aviso-campos-pendientes"
              >
                Para aprobar, completa o corrige: <strong>{camposConError.join(', ')}</strong>.
              </div>
            )}

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
              {tabActiva === 'viaje' && (
                <DatosViajeForm form={form} camposNoEncontrados={camposNoEncontrados} conductoresConocidos={conductoresConocidos} />
              )}
            </div>

            {/* Advertencias del OCR y diferencias por revisar antes de aprobar */}
            {estado !== 'guardado' && (advertenciasOcr.length > 0 || hayDiferenciaTotal) && (
              <div className="px-5 py-3 border-t border-amber-200 bg-amber-50 text-amber-900 text-xs space-y-2" data-testid="advertencias-revision">
                {advertenciasOcr.length > 0 && (
                  <ul className="list-disc pl-4 space-y-0.5">
                    {advertenciasOcr.map((a) => <li key={a}>{a}</li>)}
                  </ul>
                )}
                {hayDiferenciaTotal && (
                  <label className="flex items-start gap-2 font-semibold cursor-pointer">
                    <input
                      type="checkbox"
                      checked={revisoDiferencias}
                      onChange={(e) => setRevisoDiferencias(e.target.checked)}
                      className="mt-0.5"
                    />
                    <span>
                      Revisé las diferencias: la suma de los rubros ({formatCOP(calculo.totalGastos)}) no coincide con el
                      total escrito ({formatCOP(declarados!.totalGastos!)}); diferencia {formatCOP(diferenciaTotal.abs())}.
                    </span>
                  </label>
                )}
              </div>
            )}

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
                disabled={estado === 'guardado' || bloqueadoPorDiferencias}
                title={bloqueadoPorDiferencias ? 'Marca "Revisé las diferencias" para aprobar' : undefined}
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
