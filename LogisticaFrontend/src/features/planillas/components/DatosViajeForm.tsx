import React from 'react';
import { type UseFormReturn, Controller } from 'react-hook-form';
import type { PlanillaFormData } from '../schemas/planillaSchema';
import { Input } from '../../../components/ui/Input';
import { MoneyInput } from '../../../components/ui/MoneyInput';
import { Truck, Calendar, User, MapPin } from 'lucide-react';

export interface DatosViajeFormProps {
  form: UseFormReturn<PlanillaFormData>;
}

export const DatosViajeForm: React.FC<DatosViajeFormProps> = ({ form }) => {
  const { register, formState: { errors }, control } = form;

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
            placeholder="Ej. S14 o WCN452"
            error={errors.placa?.message}
            leftAddon={<Truck className="w-4 h-4" />}
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
            {...register('conductor')}
          />

          <Input
            label="Ciudad de Origen"
            required
            placeholder="Ej. Buenaventura"
            error={errors.origen?.message}
            leftAddon={<MapPin className="w-4 h-4" />}
            {...register('origen')}
          />

          <Input
            label="Ciudad de Destino"
            required
            placeholder="Ej. Medellín"
            error={errors.destino?.message}
            leftAddon={<MapPin className="w-4 h-4" />}
            {...register('destino')}
          />

          <Input
            label="Fecha Inicial"
            type="date"
            required
            error={errors.fechaInicial?.message}
            leftAddon={<Calendar className="w-4 h-4" />}
            {...register('fechaInicial')}
          />

          <Input
            label="Fecha Final"
            type="date"
            required
            error={errors.fechaFinal?.message}
            leftAddon={<Calendar className="w-4 h-4" />}
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
                error={errors.saldoAnterior?.message}
                helperText="Saldo a favor/contra de viajes previos"
              />
            )}
          />
        </div>
      </div>
    </div>
  );
};

