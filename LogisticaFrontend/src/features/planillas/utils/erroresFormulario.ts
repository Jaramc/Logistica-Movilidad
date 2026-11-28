import type { FieldErrors } from 'react-hook-form';
import type { PlanillaFormData } from '../schemas/planillaSchema';

/** Campos del formulario que están en la pestaña "Datos del Viaje", con el nombre que ve el usuario. */
const CAMPOS_PESTANA_VIAJE: Partial<Record<keyof PlanillaFormData, string>> = {
  origen: 'Origen',
  destino: 'Destino',
  fechaInicial: 'Fecha inicial',
  fechaFinal: 'Fecha final',
  manifiesto: 'Manifiesto',
  empresaTransportadora: 'Empresa transportadora',
  flete: 'Flete',
  anticipo: 'Anticipo',
  saldoAnterior: 'Saldo anterior',
};

/** Nombres legibles de los campos con error y la pestaña donde está el primero. */
export function resumirErrores(errores: FieldErrors<PlanillaFormData>): { pestana: 'gastos' | 'viaje'; campos: string[] } {
  const claves = Object.keys(errores) as (keyof PlanillaFormData)[];
  const campos = claves.map((k) => CAMPOS_PESTANA_VIAJE[k] ?? 'Renglones de gastos');
  const hayEnViaje = claves.some((k) => k in CAMPOS_PESTANA_VIAJE);
  return { pestana: hayEnViaje ? 'viaje' : 'gastos', campos: [...new Set(campos)] };
}
