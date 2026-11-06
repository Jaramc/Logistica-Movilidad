import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MoneyInput } from './MoneyInput';

describe('MoneyInput.tsx - Input Monetario con Precisión', () => {
  it('muestra valor formateado en COP al no estar en foco', () => {
    render(<MoneyInput label="Flete" value={1500000} onChange={vi.fn()} />);

    const input = screen.getByLabelText(/Flete/i) as HTMLInputElement;
    expect(input.value).toContain('1.500.000');
    expect(input.value).toContain('$');
  });

  it('muestra número plano al enfocar para edición ágil', () => {
    render(<MoneyInput label="Anticipo" value={2800000} onChange={vi.fn()} />);

    const input = screen.getByLabelText(/Anticipo/i) as HTMLInputElement;
    fireEvent.focus(input);
    expect(input.value).toBe('2800000');
  });

  it('emite el número exacto sin errores de coma flotante al cambiar el valor', () => {
    const onChange = vi.fn();
    render(<MoneyInput label="Gasto" value={0} onChange={onChange} />);

    const input = screen.getByLabelText(/Gasto/i) as HTMLInputElement;
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: '150.500,50' } });

    expect(onChange).toHaveBeenCalledWith(150500.50);
  });
});

