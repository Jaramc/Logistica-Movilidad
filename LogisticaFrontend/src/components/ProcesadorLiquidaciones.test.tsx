import { describe, it, expect, beforeAll, afterAll, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { crearQueryClient } from '../lib/queryClient';
import { servidor } from '../test/servidorMsw';
import ProcesadorLiquidaciones from './ProcesadorLiquidaciones';

/** Lo que devuelve /extraer para la planilla real (PEAJES ya corregido a 82.900: la suma cuadra). */
function respuestaOcr(conductor: string | null) {
  return {
    mensaje: 'Planilla procesada exitosamente.',
    data: {
      placa: 'STW514',
      origen: 'Buenaventura',
      destino: 'Medellín',
      flete: 6_976_800,
      anticipo: 2_800_000,
      totalGastos: 2_869_200,
      saldoViaje: 69_200,
      detalleGastos: [
        { concepto: 'PEAJES', valor: 82_900, etiqueta: 'PEAJES', descripcion: null, categoriaPropuesta: 'PEAJES', confianza: 0.9 },
        { concepto: 'OTROS', valor: 2_786_300, etiqueta: 'OTROS', descripcion: 'Pago tecnico mecanica', categoriaPropuesta: 'OTROS', confianza: 0.9 },
      ],
      conductor,
      fechaInicial: '2026-06-13',
      fechaFinal: '2026-06-19',
      manifiesto: null,
      empresaTransportadora: 'Cliente Ejemplo',
      advertencias: conductor ? [`Conductor tomado del nombre escrito en los saldos («${conductor}»): confirma que sea el conductor correcto y su nombre completo.`] : [],
      confianzaPromedio: 0.83,
      camposNoEncontrados: [],
      jsonOcrCrudo: '[]',
      totalGastosDeclarado: 2_869_200,
      saldoViajeDeclarado: { valor: 69_200, aFavorDe: 'Conductor' },
      saldoAnteriorDeclarado: { valor: -63_500, aFavorDe: 'Empresa' },
      saldoNuevoDeclarado: { valor: 5_700, aFavorDe: 'Conductor' },
    },
  };
}

beforeAll(() => {
  servidor.listen({ onUnhandledFrame: 'error' });
  // jsdom no implementa object URLs (el visor muestra la imagen subida).
  URL.createObjectURL = vi.fn(() => 'blob:planilla');
  URL.revokeObjectURL = vi.fn();
});
afterEach(() => {
  servidor.resetHandlers();
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
});
afterAll(() => servidor.close());

function montar() {
  return render(
    <QueryClientProvider client={crearQueryClient()}>
      <ProcesadorLiquidaciones />
    </QueryClientProvider>
  );
}

async function subirPlanilla() {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  const png = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0]);
  fireEvent.change(input, { target: { files: [new File([png], 'planilla.png', { type: 'image/png' })] } });
  await screen.findByRole('button', { name: /Aprobar y Guardar en BD/i }, { timeout: 3000 });
}

describe('ProcesadorLiquidaciones: revisión y aprobación de una planilla real', () => {
  it('completa el conductor leído en los saldos, ofrece el saldo inicial y guarda con él', async () => {
    let cuerpoGuardado: Record<string, unknown> | null = null;
    let conductoresPedidos = false;
    servidor.use(
      http.get('/api/planillas/conductores', () => {
        conductoresPedidos = true;
        return HttpResponse.json(['PEDRO PEREZ', 'ANDRES GOMEZ']);
      }),
      http.get('/api/planillas/saldo-anterior', ({ request }) => {
        const conductor = new URL(request.url).searchParams.get('conductor');
        return HttpResponse.json({ conductor, saldoAnterior: 0, planillaAnteriorId: null });
      }),
      http.post('/api/planillas/extraer', () => HttpResponse.json(respuestaOcr('Pedro'))),
      http.post('/api/planillas', async ({ request }) => {
        cuerpoGuardado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          { mensaje: 'Planilla aprobada y guardada.', id: 7, data: { placa: 'STW514', totalGastos: 2_869_200, saldoViaje: 69_200, saldoAnterior: -63_500, saldoTotal: 5_700 } },
          { status: 201 }
        );
      })
    );

    const user = userEvent.setup();
    montar();
    await waitFor(() => expect(conductoresPedidos).toBe(true));
    await subirPlanilla();

    // Primera planilla de PEDRO PEREZ en el sistema: se ofrece usar el saldo escrito (−63.500).
    const opcion = await screen.findByTestId('opcion-saldo-inicial', {}, { timeout: 3000 });
    expect(opcion).toHaveTextContent('a favor de la empresa');
    expect(opcion.querySelector('input')).toBeChecked();

    await user.click(screen.getByRole('button', { name: /Datos del Viaje/i }));
    expect(screen.getByLabelText(/Nombre del Conductor/i)).toHaveValue('PEDRO PEREZ');

    await user.click(screen.getByRole('button', { name: /Aprobar y Guardar en BD/i }));

    await waitFor(() => expect(cuerpoGuardado).not.toBeNull());
    expect(cuerpoGuardado).toMatchObject({
      conductor: 'PEDRO PEREZ',
      placa: 'STW514',
      empresaTransportadora: 'Cliente Ejemplo',
      saldoAnteriorDeclarado: -63_500,
      usarSaldoAnteriorDeclarado: true,
    });
  });

  it('si falta el conductor, al aprobar cambia a "Datos del Viaje" y dice qué falta en vez de no hacer nada', async () => {
    let guardo = false;
    servidor.use(
      http.get('/api/planillas/conductores', () => HttpResponse.json([])),
      http.post('/api/planillas/extraer', () => HttpResponse.json(respuestaOcr(null))),
      http.post('/api/planillas', () => {
        guardo = true;
        return HttpResponse.json({}, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await subirPlanilla();

    // La revisión empieza en la pestaña de gastos; el conductor está en la otra.
    expect(screen.queryByLabelText(/Nombre del Conductor/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Aprobar y Guardar en BD/i }));

    const aviso = await screen.findByTestId('aviso-campos-pendientes');
    expect(aviso).toHaveTextContent('Conductor');
    expect(screen.getByLabelText(/Nombre del Conductor/i)).toBeInTheDocument();
    expect(guardo).toBe(false);
  });
});
