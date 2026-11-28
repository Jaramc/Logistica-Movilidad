import { describe, it, expect, beforeAll, afterAll, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { crearQueryClient } from '../lib/queryClient';
import { servidor } from '../test/servidorMsw';
import ProcesadorLiquidaciones from './ProcesadorLiquidaciones';

const VIAJE = {
  id: 1,
  conductorId: 5,
  conductorCodigo: 'C05',
  conductorNombre: 'PEDRO PEREZ',
  vehiculoId: 9,
  placa: 'STW514',
  origen: 'Buenaventura',
  destino: 'Medellín',
  cliente: 'Cliente Ejemplo',
  manifiesto: null,
  flete: 6_976_800,
  fechaInicio: '2026-06-13',
  fechaFin: null,
  estado: 'EnRevision',
  saldoViaje: null,
  pendiente: null,
  anticipoTraslados: 2_800_000,
  planillaId: null,
  traslados: [],
};

function conductorConSaldo(saldo: number) {
  return { id: 5, codigo: 'C05', nombreCompleto: 'PEDRO PEREZ', cedula: '••••1234', activo: true, creadoEn: '2026-01-01', saldo, tieneMovimientos: true, partidasPendientes: [] };
}

/** Lo que devuelve /extraer para la planilla real (PEAJES ya corregido a 82.900: la suma cuadra). */
function respuestaOcr(origen: string | null = 'Buenaventura') {
  return {
    mensaje: 'Planilla procesada exitosamente.',
    data: {
      placa: 'STW514',
      origen,
      destino: 'Medellín',
      flete: 6_976_800,
      anticipo: 2_800_000,
      totalGastos: 2_869_200,
      saldoViaje: 69_200,
      detalleGastos: [
        { concepto: 'PEAJES', valor: 82_900, etiqueta: 'PEAJES', descripcion: null, categoriaPropuesta: 'PEAJES', confianza: 0.9 },
        { concepto: 'OTROS', valor: 2_786_300, etiqueta: 'OTROS', descripcion: 'Pago tecnico mecanica', categoriaPropuesta: 'OTROS', confianza: 0.9 },
      ],
      conductor: 'Pedro',
      fechaInicial: '2026-06-13',
      fechaFinal: '2026-06-19',
      manifiesto: null,
      empresaTransportadora: 'Cliente Ejemplo',
      advertencias: [],
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

function montar(saldoConductor = -63_500) {
  servidor.use(
    http.get('/api/viajes/1', () => HttpResponse.json(VIAJE)),
    http.get('/api/conductores/5', () => HttpResponse.json(conductorConSaldo(saldoConductor)))
  );
  return render(
    <QueryClientProvider client={crearQueryClient()}>
      <ProcesadorLiquidaciones viajeId={1} />
    </QueryClientProvider>
  );
}

async function subirPlanilla() {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  const png = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0]);
  fireEvent.change(input, { target: { files: [new File([png], 'planilla.png', { type: 'image/png' })] } });
  await screen.findByRole('button', { name: /Aprobar y Guardar en BD/i }, { timeout: 3000 });
}

describe('ProcesadorLiquidaciones: recibe el viaje y guarda la planilla ligada a él', () => {
  it('muestra el conductor y la placa del viaje (sin poder editarlos) y guarda con viajeId y el saldo real del conductor', async () => {
    let cuerpoGuardado: Record<string, unknown> | null = null;
    servidor.use(
      http.post('/api/planillas/extraer', () => HttpResponse.json(respuestaOcr())),
      http.post('/api/planillas', async ({ request }) => {
        cuerpoGuardado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(
          { mensaje: 'Planilla aprobada y guardada.', id: 7, data: { placa: 'STW514', totalGastos: 2_869_200, saldoViaje: 69_200, saldoAnterior: -63_500, saldoTotal: 5_700 } },
          { status: 201 }
        );
      })
    );

    const user = userEvent.setup();
    montar(-63_500);
    await screen.findByText(/C05 · PEDRO PEREZ/i);
    await subirPlanilla();

    // No hay ningún campo de texto para editar conductor o placa.
    expect(screen.queryByLabelText(/Nombre del Conductor/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Placa del Vehículo/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Aprobar y Guardar en BD/i }));

    await waitFor(() => expect(cuerpoGuardado).not.toBeNull());
    expect(cuerpoGuardado).toMatchObject({ viajeId: 1, placa: 'STW514', empresaTransportadora: 'Cliente Ejemplo' });
    expect(cuerpoGuardado).not.toHaveProperty('conductor');
    expect(cuerpoGuardado).not.toHaveProperty('usarSaldoAnteriorDeclarado');
  });

  it('avisa si el saldo anterior escrito en papel difiere del saldo real de la cuenta del conductor', async () => {
    servidor.use(
      http.post('/api/planillas/extraer', () => HttpResponse.json(respuestaOcr())),
      http.post('/api/planillas', () => HttpResponse.json({}, { status: 201 }))
    );

    // El conductor realmente tiene -100.000, pero la planilla declara -63.500.
    montar(-100_000);
    await subirPlanilla();

    const aviso = await screen.findByTestId('aviso-saldo-anterior');
    expect(aviso).toHaveTextContent('difiere del saldo actual del conductor');
  });

  it('si falta un dato obligatorio del viaje, al aprobar cambia a "Datos del Viaje" y dice qué falta', async () => {
    let guardo = false;
    servidor.use(
      http.post('/api/planillas/extraer', () => HttpResponse.json(respuestaOcr(null))),
      http.post('/api/planillas', () => {
        guardo = true;
        return HttpResponse.json({}, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await subirPlanilla();

    // La revisión empieza en la pestaña de gastos; el origen está en la otra.
    expect(screen.queryByLabelText(/Ciudad de Origen/i)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Aprobar y Guardar en BD/i }));

    const aviso = await screen.findByTestId('aviso-campos-pendientes');
    expect(aviso).toHaveTextContent('Origen');
    expect(screen.getByLabelText(/Ciudad de Origen/i)).toBeInTheDocument();
    expect(guardo).toBe(false);
  });
});
