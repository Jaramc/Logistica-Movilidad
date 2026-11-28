import React from 'react';
import { type UseFormReturn, Controller } from 'react-hook-form';
import type { PlanillaFormData } from '../schemas/planillaSchema';
import { Input } from '../../../components/ui/Input';
import { MoneyInput } from '../../../components/ui/MoneyInput';
import { Truck, Calendar, User, MapPin } from 'lucide-react';

export interface DatosViajeFormProps {
  form: UseFormReturn<PlanillaFormData>;
  /** Nombres de campo del modelo OCR ("Placa", "Flete", …) que no se encontraron en la planilla. */
  camposNoEncontrados?: readonly string[];
  /** Conductores que ya tienen planillas, para sugerirlos al escribir. */
  conductoresConocidos?: readonly string[];
}

const ID_LISTA_CONDUCTORES = 'conductores-conocidos';

const CLASE_NO_ENCONTRADO = 'border-amber-400 bg-amber-50 hover:border-amber-500';
const AYUDA_NO_ENCONTRADO = 'El OCR no encontró este dato: complétalo a mano.';

export const DatosViajeForm: React.FC<DatosViajeFormProps> = ({ form, camposNoEncontrados = [], conductoresConocidos = [] }) => {
  const { register, formState: { errors }, control } = form;

  /** Props de resaltado en ámbar para un campo que el OCR no encontró (el error de validación tiene prioridad). */
  const resaltar = (campoOcr: string, tieneError: boolean) =>
    !tieneError && camposNoEncontrados.includes(campoOcr)
      ? { className: CLASE_NO_ENCONTRADO, helperText: AYUDA_NO_ENCONTRADO, 'data-ocr-no-encontrado': true }
      : {};

  return (
    <div className="space-y-6">
      {/* Sección 1: Información de Ruta y Vehículo */}
      <div>
        <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-4 flex items-center gap-2">
          <Truck className="w-4 h-4 text-blue-600" />
          <span>Datos Principales del Viaje</span>
        </h4>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Input
            label="Placa del Vehículo"
            required
            placeholder="Ej. STW514"
            error={errors.placa?.message}
            leftAddon={<Truck className="w-4 h-4" />}
            {...resaltar('Placa', !!errors.placa)}
            {...register('placa', {
              onChange: (e) => {
                form.setValue('placa', e.target.value.toUpperCase());
              },
            })}
          />

          <Input
            label="Nombre del Conductor"
            required
            placeholder="Nombre completo"
            error={errors.conductor?.message}
            leftAddon={<User className="w-4 h-4" />}
            list={conductoresConocidos.length > 0 ? ID_LISTA_CONDUCTORES : undefined}
            autoComplete="off"
            helperText={
              errors.conductor
                ? undefined
                : 'Escríbelo igual que en sus planillas anteriores: el saldo anterior se encadena por nombre.'
            }
            {...register('conductor')}
          />
          {conductoresConocidos.length > 0 && (
            <datalist id={ID_LISTA_CONDUCTORES} data-testid="lista-conductores">
              {conductoresConocidos.map((c) => (
                <option key={c} value={c} />
              ))}
            </datalist>
          )}

          <Input
            label="Ciudad de Origen"
            required
            placeholder="Ej. Buenaventura"
            error={errors.origen?.message}
            leftAddon={<MapPin className="w-4 h-4" />}
            {...resaltar('Origen', !!errors.origen)}
            {...register('origen')}
          />

          <Input
            label="Ciudad de Destino"
            required
            placeholder="Ej. Medellín"
            error={errors.destino?.message}
            leftAddon={<MapPin className="w-4 h-4" />}
            {...resaltar('Destino', !!errors.destino)}
            {...register('destino')}
          />

          <Input
            label="Fecha Inicial"
            type="date"
            required
            error={errors.fechaInicial?.message}
            leftAddon={<Calendar className="w-4 h-4" />}
            {...resaltar('FechaInicial', !!errors.fechaInicial)}
            {...register('fechaInicial')}
          />

          <Input
            label="Fecha Final"
            type="date"
            required
            error={errors.fechaFinal?.message}
            leftAddon={<Calendar className="w-4 h-4" />}
            {...resaltar('FechaFinal', !!errors.fechaFinal)}
            {...register('fechaFinal')}
          />

          <Input
            label="N° Manifiesto de Carga"
            placeholder="Ej. MAN-8921"
            error={errors.manifiesto?.message}
            {...register('manifiesto')}
          />

          <Input
            label="Empresa Transportadora"
            placeholder="Razón social"
            error={errors.empresaTransportadora?.message}
            {...resaltar('EmpresaTransportadora', !!errors.empresaTransportadora)}
            {...register('empresaTransportadora')}
          />
        </div>
      </div>

      {/* Sección 2: Valores Económicos del Contrato */}
      <div className="pt-4 border-t border-slate-100">
        <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-4">
          Flete, Anticipo y Saldo Anterior
        </h4>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <Controller
            control={control}
            name="flete"
            render={({ field }) => (
              <MoneyInput
                label="Flete del Viaje"
                value={field.value}
                onChange={field.onChange}
                error={errors.flete?.message}
                helperText="Monto total acordado por el viaje"
                {...resaltar('Flete', !!errors.flete)}
              />
            )}
          />

          <Controller
            control={control}
            name="anticipo"
            render={({ field }) => (
              <MoneyInput
                label="Anticipo Entregado"
                value={field.value}
                onChange={field.onChange}
                error={errors.anticipo?.message}
                helperText="Dinero recibido para gastos en ruta"
                {...resaltar('Anticipo', !!errors.anticipo)}
              />
            )}
          />

          <Controller
            control={control}
            name="saldoAnterior"
            render={({ field }) => (
              <MoneyInput
                label="Saldo Anterior"
                permiteNegativos
                value={field.value}
                onChange={field.onChange}
                disabled
                error={errors.saldoAnterior?.message}
                helperText="Lo asigna el servidor: saldo total de la última planilla del conductor"
              />
            )}
          />
        </div>
      </div>
    </div>
  );
};

