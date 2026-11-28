import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { useForm } from 'react-hook-form';
import { GastosTable } from './GastosTable';
import type { PlanillaFormData } from '../schemas/planillaSchema';

function Grilla({ gastos }: { gastos: PlanillaFormData['detalleGastos'] }) {
  const form = useForm<PlanillaFormData>({ defaultValues: { detalleGastos: gastos } });
  return <GastosTable control={form.control} />;
}

describe('GastosTable — solo gastos, con etiqueta, descripción y categoría', () => {
  it('muestra la etiqueta impresa o "Libre", la nota editable y la categoría propuesta', () => {
    render(
      <Grilla
        gastos={[
          { id: 'a', etiqueta: 'COMISION', descripcion: '2 codos vigias', categoria: 'COMISION', valor: 30000 },
          { id: 'b', etiqueta: '', descripcion: 'Pago bodegaje contenedor', categoria: 'OTROS', valor: 130000 },
        ]}
      />
    );

    expect(screen.getAllByTestId('etiqueta-gasto').map((e) => e.textContent)).toEqual(['COMISION', 'Libre']);
    expect(screen.getByLabelText('Descripción del gasto 1')).toHaveValue('2 codos vigias');
    expect(screen.getByLabelText('Categoría del gasto COMISION')).toHaveValue('COMISION');
    expect(screen.getByLabelText('Categoría del gasto Pago bodegaje contenedor')).toHaveValue('OTROS');
    // Las 16 categorías en el selector.
    expect(screen.getByLabelText('Categoría del gasto COMISION').querySelectorAll('option')).toHaveLength(16);
  });
});
