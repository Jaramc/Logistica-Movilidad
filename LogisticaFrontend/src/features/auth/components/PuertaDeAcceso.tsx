import { useState } from 'react';
import { Navbar } from '../../../components/Navbar';
import { HistorialLiquidaciones } from '../../../components/HistorialLiquidaciones';
import { ErrorBoundary } from '../../../components/ErrorBoundary';
import { useSesion } from '../sesionContext';
import { pasoDeAcceso, puedeDescargarConsolidado, vistasPermitidas, type Vista } from '../permisos';
import { LoginPage } from './LoginPage';
import { Configurar2faPage } from './Configurar2faPage';
import { CambiarPasswordPage } from './CambiarPasswordPage';
import { UsuariosPage } from '../../usuarios/UsuariosPage';
import { AuditoriaPage } from '../../auditoria/AuditoriaPage';
import { ViajesPage } from '../../viajes/ViajesPage';
import { ConductoresPage } from '../../conductores/ConductoresPage';
import { VehiculosPage } from '../../vehiculos/VehiculosPage';

/** Aplicación ya autenticada (sesión con 2FA y contraseña definitiva). */
function AplicacionAutenticada() {
  const { usuario, cerrarSesion } = useSesion();
  const vistas = vistasPermitidas(usuario);
  const [vistaElegida, setVistaElegida] = useState<Vista | null>(null);
  const vistaActiva = vistaElegida && vistas.includes(vistaElegida) ? vistaElegida : vistas[0];

  if (!usuario || !vistaActiva) {
    return <p className="p-8 text-center text-slate-600">Su usuario no tiene un rol asignado. Contacte al administrador.</p>;
  }

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <Navbar
        vistas={vistas}
        vistaActiva={vistaActiva}
        setVistaActiva={setVistaElegida}
        nombreUsuario={usuario.nombreCompleto}
        rol={usuario.roles.join(', ')}
        onCerrarSesion={() => void cerrarSesion()}
      />

      <main className="flex-1">
        <ErrorBoundary fallbackTitle="Error en el módulo de liquidaciones">
          {vistaActiva === 'viajes' && <ViajesPage />}
          {vistaActiva === 'conductores' && <ConductoresPage />}
          {vistaActiva === 'vehiculos' && <VehiculosPage />}
          {vistaActiva === 'historial' && <HistorialLiquidaciones puedeDescargar={puedeDescargarConsolidado(usuario)} />}
          {vistaActiva === 'usuarios' && <UsuariosPage />}
          {vistaActiva === 'auditoria' && <AuditoriaPage />}
        </ErrorBoundary>
      </main>

      <footer className="bg-white border-t border-slate-200 py-4 text-center text-xs text-slate-500">
        Logística & Movilidad • Digitalización de Planillas de Gastos
      </footer>
    </div>
  );
}

/** Decide qué pantalla mostrar según /api/auth/me. */
export function PuertaDeAcceso() {
  const { usuario, cargando } = useSesion();

  if (cargando) {
    return <p className="p-8 text-center text-slate-500">Cargando…</p>;
  }

  switch (pasoDeAcceso(usuario)) {
    case 'login':
      return <LoginPage />;
    case 'configurar2fa':
      return <Configurar2faPage />;
    case 'cambiarPassword':
      return <CambiarPasswordPage />;
    default:
      return <AplicacionAutenticada />;
  }
}
