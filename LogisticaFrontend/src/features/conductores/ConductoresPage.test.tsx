import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { crearQueryClient } from '../../lib/queryClient';
import { servidor } from '../../test/servidorMsw';
import { ConductoresPage } from './ConductoresPage';

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  servidor.resetHandlers();
  document.cookie = 'XSRF-TOKEN=token-prueba; path=/';
});
afterAll(() => servidor.close());

const CONDUCTOR_SIN_MOVIMIENTOS = {
  id: 3, codigo: 'C03', nombreCompleto: 'CONDUCTOR PRUEBA', cedula: '••••1234', activo: true, creadoEn: '2026-01-01',
  saldo: 0, tieneMovimientos: false, partidasPendientes: [],
};

function montar() {
  document.cookie = 'XSRF-TOKEN=token-prueba; path=/';
  return render(
    <QueryClientProvider client={crearQueryClient()}>
      <ConductoresPage />
    </QueryClientProvider>
  );
}

describe('ConductoresPage: formulario de saldo inicial', () => {
  it('con dirección "La empresa le debe" envía el valor positivo', async () => {
    let cuerpoEnviado: Record<string, unknown> | null = null;
    servidor.use(
      http.get('/api/conductores', () => HttpResponse.json([CONDUCTOR_SIN_MOVIMIENTOS])),
      http.post('/api/conductores/3/saldo-inicial', async ({ request }) => {
        cuerpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: 1, tipo: 'Inicial', valor: cuerpoEnviado.valor, saldoResultante: cuerpoEnviado.valor, fechaCorte: cuerpoEnviado.fechaCorte, justificacion: cuerpoEnviado.justificacion, viajeId: null, pagoSaldoId: null, creadoEn: '2026-01-01' }, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await user.click(await screen.findByRole('button', { name: 'Saldo inicial' }));

    const valorInput = await screen.findByLabelText('Valor');
    await user.clear(valorInput);
    await user.type(valorInput, '63500');
    // "EmpresaDebe" ya es la opción por defecto.
    await user.type(screen.getByLabelText(/^Justificación/), 'Saldo inicial de prueba');
    await user.click(screen.getByRole('button', { name: 'Guardar saldo inicial' }));

    await waitFor(() => expect(cuerpoEnviado).not.toBeNull());
    expect(cuerpoEnviado).toMatchObject({ valor: 63_500 });
  });

  it('con dirección "El conductor debe" envía el valor negativo', async () => {
    let cuerpoEnviado: Record<string, unknown> | null = null;
    servidor.use(
      http.get('/api/conductores', () => HttpResponse.json([CONDUCTOR_SIN_MOVIMIENTOS])),
      http.post('/api/conductores/3/saldo-inicial', async ({ request }) => {
        cuerpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({}, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await user.click(await screen.findByRole('button', { name: 'Saldo inicial' }));

    const valorInput = await screen.findByLabelText('Valor');
    await user.clear(valorInput);
    await user.type(valorInput, '63500');
    await user.selectOptions(screen.getByTestId('direccion-saldo-inicial'), 'ConductorDebe');
    await user.type(screen.getByLabelText(/^Justificación/), 'Saldo inicial de prueba');
    await user.click(screen.getByRole('button', { name: 'Guardar saldo inicial' }));

    await waitFor(() => expect(cuerpoEnviado).not.toBeNull());
    expect(cuerpoEnviado).toMatchObject({ valor: -63_500 });
  });
});
