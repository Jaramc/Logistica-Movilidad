import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { crearQueryClient } from '../../lib/queryClient';
import { SesionProvider } from './SesionProvider';
import { PuertaDeAcceso } from './components/PuertaDeAcceso';
import { servidor, usuarioDePrueba, problema, sinSesion } from '../../test/servidorMsw';
import type { UsuarioActual } from './authApi';

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
  servidor.resetHandlers();
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
});
afterAll(() => servidor.close());

function renderApp() {
  return render(
    <QueryClientProvider client={crearQueryClient()}>
      <SesionProvider>
        <PuertaDeAcceso />
      </SesionProvider>
    </QueryClientProvider>
  );
}

/** "me" responde según una variable: así el test simula que la sesión cambia en el servidor. */
function meDinamico(estado: { usuario: UsuarioActual | null }) {
  return http.get('/api/auth/me', () => (estado.usuario ? HttpResponse.json(estado.usuario) : sinSesion()));
}

const historialVacio = http.get('/api/planillas', () =>
  HttpResponse.json({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
);

describe('Flujo de login', () => {
  it('sin sesión muestra el login; con credenciales y código 2FA entra a la aplicación', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: null };
    let tokenRecibido: string | null = null;
    servidor.use(
      meDinamico(estado),
      historialVacio,
      http.post('/api/auth/login', async ({ request }) => {
        tokenRecibido = request.headers.get('X-XSRF-TOKEN');
        const cuerpo = (await request.json()) as { email: string; password: string };
        return cuerpo.password === 'frase-larga-correcta'
          ? HttpResponse.json({ requiere2fa: true })
          : problema(401, 'Usuario o contraseña incorrectos.');
      }),
      http.post('/api/auth/login-2fa', async ({ request }) => {
        const { codigo } = (await request.json()) as { codigo: string };
        if (codigo !== '123456') return problema(401, 'El código no es válido o la verificación expiró. Inicie sesión de nuevo.');
        estado.usuario = usuarioDePrueba();
        return HttpResponse.json(estado.usuario);
      })
    );

    const user = userEvent.setup();
    renderApp();

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument();

    // Contraseña incorrecta: mensaje genérico.
    await user.type(screen.getByLabelText(/Correo/), 'ana@empresa.co');
    await user.type(screen.getByLabelText(/Contraseña/), 'mala');
    await user.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(await screen.findByText('Usuario o contraseña incorrectos.')).toBeInTheDocument();
    expect(tokenRecibido).toBe('token-prueba');

    // Contraseña correcta → paso del código.
    await user.type(screen.getByLabelText(/Contraseña/), 'frase-larga-correcta');
    await user.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(await screen.findByRole('heading', { name: 'Verificación en dos pasos' })).toBeInTheDocument();

    await user.type(screen.getByLabelText(/Código de 6 dígitos/), '123456');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('button', { name: /Cerrar sesión/ })).toBeInTheDocument();
    expect(screen.getByText('Ana Gómez')).toBeInTheDocument();
  });

  it('permite entrar con un código de recuperación', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: null };
    servidor.use(
      meDinamico(estado),
      historialVacio,
      http.post('/api/auth/login', () => HttpResponse.json({ requiere2fa: true })),
      http.post('/api/auth/login-recuperacion', async ({ request }) => {
        const { codigo } = (await request.json()) as { codigo: string };
        if (codigo !== 'ABCDE-12345') return problema(401, 'Código inválido.');
        estado.usuario = usuarioDePrueba();
        return HttpResponse.json(estado.usuario);
      })
    );
    const user = userEvent.setup();
    renderApp();

    await user.type(await screen.findByLabelText(/Correo/), 'ana@empresa.co');
    await user.type(screen.getByLabelText(/Contraseña/), 'frase-larga-correcta');
    await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.click(await screen.findByRole('button', { name: 'Usar un código de recuperación' }));
    await user.type(screen.getByLabelText(/Código de recuperación/), 'ABCDE-12345');
    await user.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('button', { name: /Cerrar sesión/ })).toBeInTheDocument();
  });

  it('un usuario sin 2FA debe configurarlo: el QR se genera localmente y se muestran los códigos una vez', async () => {
    const estado: { usuario: UsuarioActual | null } = {
      usuario: usuarioDePrueba({ tiene2fa: false, sesionConMfa: false }),
    };
    servidor.use(
      meDinamico(estado),
      historialVacio,
      http.post('/api/auth/2fa/configurar', () =>
        HttpResponse.json({ clave: 'abcd efgh ijkl', uriOtpauth: 'otpauth://totp/Logistica%20LM:ana%40empresa.co?secret=ABCDEFGHIJKL&issuer=Logistica%20LM' })
      ),
      http.post('/api/auth/2fa/verificar', () => {
        estado.usuario = usuarioDePrueba();
        return HttpResponse.json({ codigos: ['AAAAA-11111', 'BBBBB-22222'] });
      })
    );
    const user = userEvent.setup();
    const { container } = renderApp();

    await user.click(await screen.findByRole('button', { name: 'Generar código QR' }));
    expect(await screen.findByTestId('clave-2fa')).toHaveTextContent('abcd efgh ijkl');
    // QR dibujado como SVG en el navegador; ninguna imagen remota.
    expect(container.querySelector('svg')).not.toBeNull();
    expect(container.querySelector('img[src^="http"]')).toBeNull();

    await user.type(screen.getByLabelText(/Código de 6 dígitos/), '654321');
    await user.click(screen.getByRole('button', { name: 'Activar' }));

    const lista = await screen.findByRole('list', { name: 'Códigos de recuperación' });
    expect(within(lista).getAllByRole('listitem')).toHaveLength(2);

    await user.click(screen.getByRole('button', { name: 'Ya los guardé' }));
    expect(await screen.findByRole('button', { name: /Cerrar sesión/ })).toBeInTheDocument();
  });

  it('con contraseña temporal obliga a cambiarla antes de entrar', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: usuarioDePrueba({ debeCambiarPassword: true }) };
    servidor.use(
      meDinamico(estado),
      historialVacio,
      http.post('/api/auth/cambiar-password', () => {
        estado.usuario = usuarioDePrueba();
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp();

    expect(await screen.findByRole('heading', { name: 'Cambie su contraseña' })).toBeInTheDocument();
    await user.type(screen.getByLabelText(/Contraseña actual/), 'Temporal-1234567');
    await user.type(screen.getByLabelText(/^Contraseña nueva/), 'caballo bateria grapa');
    await user.type(screen.getByLabelText(/Confirme la contraseña nueva/), 'caballo bateria grapa');
    await user.click(screen.getByRole('button', { name: 'Guardar contraseña' }));

    expect(await screen.findByRole('button', { name: /Cerrar sesión/ })).toBeInTheDocument();
  });
});

describe('Sesión expirada', () => {
  it('ante un 401 limpia el estado y vuelve al login con "Tu sesión expiró"', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: usuarioDePrueba() };
    servidor.use(
      meDinamico(estado),
      http.get('/api/viajes', () => {
        estado.usuario = null; // el servidor ya no reconoce la cookie
        return sinSesion();
      })
    );
    renderApp();

    expect(await screen.findByText(/Tu sesión expiró/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument();
  });

  it('no guarda nada de la sesión en localStorage ni sessionStorage', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: usuarioDePrueba() };
    servidor.use(meDinamico(estado), historialVacio);
    renderApp();
    await screen.findByRole('button', { name: /Cerrar sesión/ });

    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });

  it('cerrar sesión llama al backend y vuelve al login', async () => {
    const estado: { usuario: UsuarioActual | null } = { usuario: usuarioDePrueba() };
    let logout = false;
    servidor.use(
      meDinamico(estado),
      historialVacio,
      http.post('/api/auth/logout', () => {
        logout = true;
        estado.usuario = null;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp();

    await user.click(await screen.findByRole('button', { name: /Cerrar sesión/ }));
    await waitFor(() => expect(logout).toBe(true));
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument();
  });
});

describe('Menú según el rol', () => {
  async function botonesDelMenu(roles: UsuarioActual['roles']) {
    servidor.use(
      http.get('/api/auth/me', () => HttpResponse.json(usuarioDePrueba({ roles }))),
      historialVacio
    );
    renderApp();
    const menu = await screen.findByRole('navigation', { name: 'Menú principal' });
    return within(menu)
      .getAllByRole('button')
      .map((b) => b.textContent?.trim())
      .filter((t) => t !== 'Cerrar sesión');
  }

  it('Consulta ve viajes, conductores, vehículos e historial, pero no la descarga', async () => {
    expect(await botonesDelMenu(['Consulta'])).toEqual(['Viajes', 'Conductores', 'Vehículos', 'Historial']);
    expect(screen.queryByRole('button', { name: /Descargar Consolidado/ })).toBeNull();
  });

  it('Revisor ve lo mismo que Consulta (sin usuarios ni auditoría)', async () => {
    expect(await botonesDelMenu(['Revisor'])).toEqual(['Viajes', 'Conductores', 'Vehículos', 'Historial']);
  });

  it('Administrador ve además usuarios, auditoría y la descarga del Excel', async () => {
    const user = userEvent.setup();
    expect(await botonesDelMenu(['Administrador'])).toEqual(['Viajes', 'Conductores', 'Vehículos', 'Historial', 'Usuarios', 'Auditoría']);
    await user.click(screen.getByRole('button', { name: /Historial/ }));
    expect(await screen.findByRole('button', { name: /Descargar Consolidado/ })).toBeInTheDocument();
  });
});
