import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import Big from 'big.js';
import { SaldosDeclarados } from './SaldosDeclarados';

describe('SaldosDeclarados — lo escrito en papel junto al cálculo', () => {
  const calculo = { totalGastos: new Big(2819200), saldoViaje: new Big(19200), saldoTotal: new Big(-44300) };

  it('muestra los saldos con a favor de quién y marca en ámbar los que difieren', () => {
    render(
      <SaldosDeclarados
        totalGastos={2869200}
        saldoViaje={{ valor: 69200, aFavorDe: 'Conductor' }}
        saldoAnterior={{ valor: -63500, aFavorDe: 'Empresa' }}
        saldoNuevo={{ valor: 5700, aFavorDe: 'Conductor' }}
        calculo={calculo}
        saldoAnteriorServidor={-63500}
      />
    );

    expect(screen.getByText('Saldos declarados en la planilla')).toBeInTheDocument();
    expect(screen.getByTestId('declarado-total-gastos')).toHaveAttribute('data-difiere', 'true');
    expect(screen.getByTestId('declarado-saldo-viaje')).toHaveAttribute('data-difiere', 'true');
    expect(screen.getByTestId('declarado-saldo-anterior')).not.toHaveAttribute('data-difiere');
    expect(screen.getByTestId('declarado-saldo-anterior').textContent).toMatch(/63\.500.*a favor de: Empresa/);
    expect(screen.getByTestId('declarado-saldo-nuevo').textContent).toMatch(/Sistema: .*44\.300 a favor de: Empresa/);
  });

  it('no se muestra si la planilla no trae saldos escritos', () => {
    const { container } = render(
      <SaldosDeclarados totalGastos={null} saldoViaje={null} saldoAnterior={null} saldoNuevo={null}
        calculo={calculo} saldoAnteriorServidor={null} />
    );
    expect(container).toBeEmptyDOMElement();
  });
});
