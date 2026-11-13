import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/types';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { Input } from '../../components/ui/Input';
import type { Rol } from '../auth/authApi';
import { useSesion } from '../auth/sesionContext';
import { cambiarEstado, cambiarRol, crearUsuario, listarUsuarios, regenerarPasswordTemporal, reiniciar2fa, ROLES, type UsuarioCreado } from './usuariosApi';

const CLAVE = ['usuarios'];

function textoError(error: unknown): string {
  if (error instanceof ApiError) {
    const campos = error.fieldErrors ? Object.values(error.fieldErrors).flat().join(' ') : '';
    return campos || error.getMensajeUsuario();
  }
  return 'No se pudo completar la operación.';
}

function fecha(valor: string | null): string {
  return valor ? new Date(valor).toLocaleString('es-CO') : '—';
}

export function UsuariosPage() {
  const { usuario: actual } = useSesion();
  const queryClient = useQueryClient();
  const { data: usuarios = [], isLoading, error } = useQuery({ queryKey: CLAVE, queryFn: listarUsuarios });

  const [email, setEmail] = useState('');
  const [nombre, setNombre] = useState('');
  const [rol, setRol] = useState<Rol>('Consulta');
  const [creado, setCreado] = useState<UsuarioCreado | null>(null);
  const [errorAccion, setErrorAccion] = useState<string | null>(null);

  const refrescar = () => queryClient.invalidateQueries({ queryKey: CLAVE });
  const alFallar = (e: unknown) => setErrorAccion(textoError(e));

  const crear = useMutation({
    mutationFn: crearUsuario,
    onSuccess: (r) => {
      setCreado(r);
      setEmail('');
      setNombre('');
      setRol('Consulta');
      void refrescar();
    },
    onError: alFallar,
  });
  const rolMut = useMutation({ mutationFn: (v: { id: string; rol: Rol }) => cambiarRol(v.id, v.rol), onSuccess: refrescar, onError: alFallar });
  const estadoMut = useMutation({ mutationFn: (v: { id: string; activo: boolean }) => cambiarEstado(v.id, v.activo), onSuccess: refrescar, onError: alFallar });
  const reiniciarMut = useMutation({ mutationFn: reiniciar2fa, onSuccess: refrescar, onError: alFallar });
  const temporalMut = useMutation({
    mutationFn: regenerarPasswordTemporal,
    onSuccess: (r) => {
      setCreado(r);
      void refrescar();
    },
    onError: alFallar,
  });

  const enviar = (e: FormEvent) => {
    e.preventDefault();
    setErrorAccion(null);
    setCreado(null);
    crear.mutate({ email: email.trim(), nombreCompleto: nombre.trim(), rol });
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-slate-900">Usuarios</h2>
        <p className="text-sm text-slate-500">Solo un administrador crea cuentas. Cada usuario debe configurar su 2FA al entrar.</p>
      </div>

      {errorAccion && (
        <Alert variant="error" onDismiss={() => setErrorAccion(null)}>
          {errorAccion}
        </Alert>
      )}

      {creado && (
        <Alert variant="success" title="Contraseña temporal generada" onDismiss={() => setCreado(null)}>
          Entregue esta contraseña temporal a <strong>{creado.usuario.email}</strong> por un canal seguro. Se muestra una sola
          vez, sirve para un solo inicio de sesión y vence en 24 horas:
          <code className="block mt-2 font-mono text-base bg-white border border-emerald-200 rounded px-2 py-1 select-all" data-testid="password-temporal">
            {creado.passwordTemporal}
          </code>
        </Alert>
      )}

      <form onSubmit={enviar} className="bg-white border border-slate-200 rounded-xl p-4 grid gap-3 sm:grid-cols-4 items-end">
        <Input label="Correo" type="email" value={email} onChange={(e) => setEmail(e.target.value)} maxLength={254} required />
        <Input label="Nombre completo" value={nombre} onChange={(e) => setNombre(e.target.value)} maxLength={150} required />
        <label className="flex flex-col text-sm font-medium text-slate-700">
          Rol
          <select value={rol} onChange={(e) => setRol(e.target.value as Rol)} className="mt-1 rounded-md border border-slate-300 px-2 py-2 text-sm">
            {ROLES.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </label>
        <Button type="submit" isLoading={crear.isPending} disabled={!email || !nombre}>
          Crear usuario
        </Button>
      </form>

      {error && <Alert variant="error">{textoError(error)}</Alert>}

      <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-2">Usuario</th>
              <th className="px-4 py-2">Rol</th>
              <th className="px-4 py-2">Estado</th>
              <th className="px-4 py-2">2FA</th>
              <th className="px-4 py-2">Último acceso</th>
              <th className="px-4 py-2">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-500">
                  Cargando…
                </td>
              </tr>
            )}
            {usuarios.map((u) => {
              const esYo = u.id === actual?.id;
              return (
                <tr key={u.id}>
                  <td className="px-4 py-2">
                    <div className="font-medium text-slate-900">{u.nombreCompleto}</div>
                    <div className="text-xs text-slate-500">{u.email}</div>
                  </td>
                  <td className="px-4 py-2">
                    <select
                      aria-label={`Rol de ${u.email}`}
                      value={u.rol ?? ''}
                      onChange={(e) => rolMut.mutate({ id: u.id, rol: e.target.value as Rol })}
                      className="rounded-md border border-slate-300 px-2 py-1 text-xs"
                    >
                      {ROLES.map((r) => (
                        <option key={r} value={r}>
                          {r}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="px-4 py-2 text-xs">
                    {!u.activo ? 'Inactivo' : u.pendienteActivacion ? 'Pendiente de activación' : 'Activo'}
                    {u.bloqueado && u.activo && ' · Bloqueado'}
                    {u.debeCambiarPassword && !u.passwordTemporalInutilizable && ' · Contraseña temporal vigente'}
                    {u.passwordTemporalInutilizable && (
                      <span className="text-rose-700"> · Contraseña temporal vencida o usada</span>
                    )}
                  </td>
                  <td className="px-4 py-2 text-xs">{u.tiene2fa ? 'Configurado' : 'Pendiente'}</td>
                  <td className="px-4 py-2 text-xs">{fecha(u.ultimoAcceso)}</td>
                  <td className="px-4 py-2 space-x-2 whitespace-nowrap">
                    <Button
                      size="sm"
                      variant={u.activo ? 'danger' : 'success'}
                      disabled={esYo && u.activo}
                      title={esYo ? 'No puede desactivar su propia cuenta' : undefined}
                      onClick={() => {
                        if (!u.activo || window.confirm(`¿Desactivar a ${u.email}? Sus sesiones abiertas se cerrarán.`)) {
                          estadoMut.mutate({ id: u.id, activo: !u.activo });
                        }
                      }}
                    >
                      {u.activo ? 'Desactivar' : 'Activar'}
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={!u.tiene2fa}
                      onClick={() => {
                        if (window.confirm(`¿Reiniciar el 2FA de ${u.email}? Deberá configurarlo de nuevo.`)) reiniciarMut.mutate(u.id);
                      }}
                    >
                      Reiniciar 2FA
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        if (window.confirm(`¿Generar una contraseña temporal nueva para ${u.email}? Sus sesiones se cerrarán.`)) {
                          temporalMut.mutate(u.id);
                        }
                      }}
                    >
                      Nueva contraseña temporal
                    </Button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
