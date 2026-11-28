import { describe, it, expect } from 'vitest';
import type { FieldErrors } from 'react-hook-form';
import type { PlanillaFormData } from '../schemas/planillaSchema';
import { resumirErrores } from './erroresFormulario';

const error = { type: 'too_small', message: 'requerido' };

describe('resumirErrores', () => {
  it('lleva a "Datos del Viaje" si falta el conductor aunque también fallen los gastos', () => {
    const errores = { conductor: error, detalleGastos: error } as unknown as FieldErrors<PlanillaFormData>;
    expect(resumirErrores(errores)).toEqual({ pestana: 'viaje', campos: ['Conductor', 'Renglones de gastos'] });
  });

  it('se queda en gastos si solo fallan los renglones', () => {
    const errores = { detalleGastos: [{ concepto: error }] } as unknown as FieldErrors<PlanillaFormData>;
    expect(resumirErrores(errores)).toEqual({ pestana: 'gastos', campos: ['Renglones de gastos'] });
  });
});
