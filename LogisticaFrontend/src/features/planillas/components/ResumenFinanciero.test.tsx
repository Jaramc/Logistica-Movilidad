import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ResumenFinanciero } from './ResumenFinanciero';
import Big from 'big.js';

describe('ResumenFinanciero.tsx - Tarjetas y Semántica Accesible', () => {
  it('renderiza el balance económico con montos formateados en COP y etiquetas descriptivas', () => {
    const calculoMock = {
      totalGastos: new Big(2869200),
      saldoViaje: new Big(69200),
      saldoTotal: new Big(79200),
    };

    render(
      <ResumenFinanciero
        flete={6976800}
        anticipo={2800000}
        calculo={calculoMock}
        cantidadGastos={4}
      />
    );

    expect(screen.getByText(/Flete Acordado/i)).toBeInTheDocument();
    expect(screen.getByText(/Anticipo Entregado/i)).toBeInTheDocument();
    expect(screen.getByText(/Total Gastos \(4\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Saldo del Viaje/i)).toBeInTheDocument();
    expect(screen.getByText(/Saldo a Favor del Conductor/i)).toBeInTheDocument();
  });

  it('muestra etiqueta semántica de reintegro a la empresa cuando el saldo de viaje es negativo', () => {
    const calculoMock = {
      totalGastos: new Big(2000000),
      saldoViaje: new Big(-500000),
      saldoTotal: new Big(-500000),
    };

    render(
      <ResumenFinanciero
        flete={5000000}
        anticipo={2500000}
        calculo={calculoMock}
        cantidadGastos={1}
      />
    );

    expect(screen.getByText(/Reintegro a la Empresa \(Sobrante\)/i)).toBeInTheDocument();
  });
});

