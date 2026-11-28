import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import type { UsuarioActual } from '../features/auth/authApi';

/** Servidor MSW compartido por las pruebas de autenticación. Cada prueba define sus handlers. */
export const servidor = setupServer(
  // El backend entrega la cookie XSRF-TOKEN; en jsdom la simulamos escribiendo document.cookie.
  http.get('/api/auth/antiforgery', () => {
    document.cookie = 'XSRF-TOKEN=token-prueba; path=/';
    return new HttpResponse(null, { status: 204 });
  })
);

export function usuarioDePrueba(cambios: Partial<UsuarioActual> = {}): UsuarioActual {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    email: 'ana@empresa.co',
    nombreCompleto: 'Ana Gómez',
    roles: ['Consulta'],
    tiene2fa: true,
    sesionConMfa: true,
    debeCambiarPassword: false,
    ...cambios,
  };
}

export const problema = (status: number, detail: string, headers: Record<string, string> = {}) =>
  HttpResponse.json({ status, title: 'Error', detail }, { status, headers: { 'content-type': 'application/problem+json', ...headers } });

export const sinSesion = () => problema(401, 'Debe iniciar sesión.');
