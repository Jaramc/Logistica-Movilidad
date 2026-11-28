import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { crearQueryClient } from '../../lib/queryClient';
import { servidor, problema } from '../../test/servidorMsw';
import { SesionContext, type EstadoSesion } from '../auth/sesionContext';
import { ViajesPage } from './ViajesPage';

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  servidor.resetHandlers();
  document.cookie = 'XSRF-TOKEN=token-prueba; path=/';
});
afterAll(() => servidor.close());

const SESION_ADMIN: EstadoSesion = {
  usuario: {
    id: '1', email: 'admin@empresa.co', nombreCompleto: 'Admin', roles: ['Administrador'],
    tiene2fa: true, sesionConMfa: true, debeCambiarPassword: false,
  },
  cargando: false,
  mensaje: null,
  refrescar: async () => {},
  cerrarSesion: async () => {},
};

function montar() {
  document.cookie = 'XSRF-TOKEN=token-prueba; path=/';
  return render(
    <QueryClientProvider client={crearQueryClient()}>
      <SesionContext.Provider value={SESION_ADMIN}>
        <ViajesPage />
      </SesionContext.Provider>
    </QueryClientProvider>
  );
}

const VIAJE_BASE = {
  id: 1, conductorId: 5, conductorCodigo: 'C05', conductorNombre: 'PEDRO PEREZ', vehiculoId: 9, placa: 'STW514',
  origen: 'Buenaventura', destino: 'Medellín', cliente: 'Cliente Ejemplo', manifiesto: null, flete: 6_976_800,
  fechaInicio: '2026-06-13', fechaFin: null, estado: 'Abierto', saldoViaje: null, pendiente: null,
  anticipoTraslados: 0, planillaId: null, traslados: [],
};

describe('ViajesPage: abrir un viaje', () => {
  it('envía conductorId, vehiculoId y los datos del formulario a POST /api/viajes', async () => {
    let cuerpoEnviado: Record<string, unknown> | null = null;
    servidor.use(
      http.get('/api/viajes', () => HttpResponse.json({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })),
      http.get('/api/conductores', () => HttpResponse.json([{ id: 5, codigo: 'C05', nombreCompleto: 'PEDRO PEREZ', cedula: '••••1234', activo: true, creadoEn: '2026-01-01', saldo: 0, tieneMovimientos: false, partidasPendientes: [] }])),
      http.get('/api/vehiculos', () => HttpResponse.json([{ id: 9, placa: 'STW514', tipo: 'Tractomula', categoriaPeaje: null, activo: true }])),
      http.post('/api/viajes', async ({ request }) => {
        cuerpoEnviado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...VIAJE_BASE }, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await user.click(await screen.findByRole('button', { name: 'Abrir viaje' }));

    await user.selectOptions(await screen.findByLabelText('Conductor'), '5');
    await user.selectOptions(screen.getByLabelText('Vehículo'), '9');
    await user.type(screen.getByLabelText(/^Cliente/), 'Cliente Ejemplo');
    await user.type(screen.getByLabelText(/^Origen/), 'Buenaventura');
    await user.type(screen.getByLabelText(/^Destino/), 'Medellín');

    // Hay dos botones "Abrir viaje": el que abre el formulario y el de enviarlo (el último).
    const botones = screen.getAllByRole('button', { name: 'Abrir viaje' });
    await user.click(botones[botones.length - 1]!);

    await waitFor(() => expect(cuerpoEnviado).not.toBeNull());
    expect(cuerpoEnviado).toMatchObject({ conductorId: 5, vehiculoId: 9, cliente: 'Cliente Ejemplo', origen: 'Buenaventura', destino: 'Medellín' });
  });
});

describe('ViajesPage: registrar un traslado', () => {
  it('lo agrega al viaje abierto', async () => {
    let cuerpoTraslado: Record<string, unknown> | null = null;
    servidor.use(
      http.get('/api/viajes', () => HttpResponse.json({ items: [VIAJE_BASE], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 })),
      http.get('/api/viajes/1', () => HttpResponse.json(VIAJE_BASE)),
      http.post('/api/viajes/1/traslados', async ({ request }) => {
        cuerpoTraslado = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: 1, fecha: cuerpoTraslado.fecha, valor: cuerpoTraslado.valor, medio: cuerpoTraslado.medio, nota: null, creadoEn: '2026-06-13T00:00:00Z' }, { status: 201 });
      })
    );

    const user = userEvent.setup();
    montar();
    await user.click(await screen.findByRole('button', { name: 'Ver' }));
    await user.click(await screen.findByRole('button', { name: 'Registrar traslado' }));

    const valorInput = screen.getByLabelText('Valor');
    await user.clear(valorInput);
    await user.type(valorInput, '2800000');
    await user.click(screen.getByRole('button', { name: 'Guardar traslado' }));

    await waitFor(() => expect(cuerpoTraslado).not.toBeNull());
    expect(cuerpoTraslado).toMatchObject({ valor: 2_800_000, medio: 'Transferencia' });
  });
});

describe('ViajesPage: aprobar sin planilla', () => {
  it('muestra el error 422 del servidor', async () => {
    servidor.use(
      http.get('/api/viajes', () => HttpResponse.json({ items: [VIAJE_BASE], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 })),
      http.get('/api/viajes/1', () => HttpResponse.json(VIAJE_BASE)),
      http.post('/api/viajes/1/aprobar', () =>
        problema(422, 'El viaje debe tener una planilla guardada y al menos un traslado registrado para poder aprobarse.')
      )
    );

    const user = userEvent.setup();
    montar();
    await user.click(await screen.findByRole('button', { name: 'Ver' }));
    await user.click(await screen.findByRole('button', { name: 'Aprobar viaje' }));

    expect(await screen.findByText(/debe tener una planilla guardada/i)).toBeInTheDocument();
  });
});
