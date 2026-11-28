import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/types';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { Input } from '../../components/ui/Input';
import { MoneyInput } from '../../components/ui/MoneyInput';
import { describirSaldo } from '../planillas/utils/money';
import { hoyLocalISO } from '../../lib/fechas';
import {
  actualizarConductor,
  crearConductor,
  desactivarConductor,
  obtenerMovimientos,
  registrarSaldoInicial,
  listarConductores,
  type Conductor,
} from './conductoresApi';

const CLAVE = ['conductores'];

function textoError(error: unknown): string {
  if (error instanceof ApiError) {
    const campos = error.fieldErrors ? Object.values(error.fieldErrors).flat().join(' ') : '';
    return campos || error.getMensajeUsuario();
  }
  return 'No se pudo completar la operación.';
}

function TarjetaSaldo({ saldo }: { saldo: number }) {
  const color = saldo > 0 ? 'text-emerald-700' : saldo < 0 ? 'text-rose-700' : 'text-slate-500';
  return <span className={`font-mono font-semibold ${color}`}>{describirSaldo(saldo)}</span>;
}

export function ConductoresPage() {
  const queryClient = useQueryClient();
  const { data: conductores = [], isLoading, error } = useQuery({ queryKey: CLAVE, queryFn: () => listarConductores() });

  const [nombreCompleto, setNombreCompleto] = useState('');
  const [cedula, setCedula] = useState('');
  const [errorAccion, setErrorAccion] = useState<string | null>(null);
  const [editando, setEditando] = useState<Conductor | null>(null);
  const [nombreEdicion, setNombreEdicion] = useState('');
  const [saldoInicialPara, setSaldoInicialPara] = useState<Conductor | null>(null);
  const [movimientosDe, setMovimientosDe] = useState<Conductor | null>(null);

  const refrescar = () => queryClient.invalidateQueries({ queryKey: CLAVE });
  const alFallar = (e: unknown) => setErrorAccion(textoError(e));

  const crear = useMutation({
    mutationFn: crearConductor,
    onSuccess: () => {
      setNombreCompleto('');
      setCedula('');
      void refrescar();
    },
    onError: alFallar,
  });

  const actualizar = useMutation({
    mutationFn: (v: { id: number; nombreCompleto: string }) => actualizarConductor(v.id, { nombreCompleto: v.nombreCompleto }),
    onSuccess: () => {
      setEditando(null);
      void refrescar();
    },
    onError: alFallar,
  });

  const desactivar = useMutation({ mutationFn: desactivarConductor, onSuccess: refrescar, onError: alFallar });

  const enviarCreacion = (e: FormEvent) => {
    e.preventDefault();
    setErrorAccion(null);
    crear.mutate({ nombreCompleto: nombreCompleto.trim(), cedula: cedula.trim() });
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-slate-900">Conductores</h2>
        <p className="text-sm text-slate-500">
          El código y el saldo los administra el sistema. Nunca se borra un conductor: solo se desactiva.
        </p>
      </div>

      {errorAccion && (
        <Alert variant="error" onDismiss={() => setErrorAccion(null)}>
          {errorAccion}
        </Alert>
      )}
      {error && <Alert variant="error">{textoError(error)}</Alert>}

      <form onSubmit={enviarCreacion} className="bg-white border border-slate-200 rounded-xl p-4 grid gap-3 sm:grid-cols-4 items-end">
        <Input label="Nombre completo" value={nombreCompleto} onChange={(e) => setNombreCompleto(e.target.value)} maxLength={150} required />
        <Input
          label="Cédula"
          value={cedula}
          onChange={(e) => setCedula(e.target.value.replace(/\D/g, ''))}
          minLength={6}
          maxLength={12}
          required
          helperText="Solo dígitos, entre 6 y 12."
        />
        <Button type="submit" isLoading={crear.isPending} disabled={!nombreCompleto || !cedula}>
          Crear conductor
        </Button>
      </form>

      <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-2">Código</th>
              <th className="px-4 py-2">Nombre</th>
              <th className="px-4 py-2">Cédula</th>
              <th className="px-4 py-2">Estado</th>
              <th className="px-4 py-2">Saldo</th>
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
            {conductores.map((c) => (
              <tr key={c.id}>
                <td className="px-4 py-2 font-mono text-xs">{c.codigo}</td>
                <td className="px-4 py-2">
                  {editando?.id === c.id ? (
                    <input
                      value={nombreEdicion}
                      onChange={(e) => setNombreEdicion(e.target.value)}
                      className="rounded-md border border-slate-300 px-2 py-1 text-sm w-full"
                    />
                  ) : (
                    c.nombreCompleto
                  )}
                </td>
                <td className="px-4 py-2 font-mono text-xs">{c.cedula}</td>
                <td className="px-4 py-2 text-xs">{c.activo ? 'Activo' : 'Inactivo'}</td>
                <td className="px-4 py-2">
                  <TarjetaSaldo saldo={c.saldo} />
                </td>
                <td className="px-4 py-2 space-x-2 whitespace-nowrap">
                  {editando?.id === c.id ? (
                    <>
                      <Button
                        size="sm"
                        isLoading={actualizar.isPending}
                        onClick={() => actualizar.mutate({ id: c.id, nombreCompleto: nombreEdicion.trim() })}
                      >
                        Guardar
                      </Button>
                      <Button size="sm" variant="outline" onClick={() => setEditando(null)}>
                        Cancelar
                      </Button>
                    </>
                  ) : (
                    <>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setEditando(c);
                          setNombreEdicion(c.nombreCompleto);
                        }}
                      >
                        Editar
                      </Button>
                      {!c.tieneMovimientos && (
                        <Button size="sm" variant="secondary" onClick={() => setSaldoInicialPara(c)}>
                          Saldo inicial
                        </Button>
                      )}
                      <Button size="sm" variant="outline" onClick={() => setMovimientosDe(c)}>
                        Movimientos
                      </Button>
                      <Button
                        size="sm"
                        variant={c.activo ? 'danger' : 'success'}
                        onClick={() => {
                          if (window.confirm(`¿${c.activo ? 'Desactivar' : 'Activar'} a ${c.nombreCompleto}?`)) {
                            desactivar.mutate(c.id);
                          }
                        }}
                      >
                        {c.activo ? 'Desactivar' : 'Activar'}
                      </Button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {saldoInicialPara && (
        <DialogoSaldoInicial
          conductor={saldoInicialPara}
          onCerrar={() => setSaldoInicialPara(null)}
          onGuardado={() => {
            setSaldoInicialPara(null);
            void refrescar();
          }}
        />
      )}

      {movimientosDe && <PanelMovimientos conductor={movimientosDe} onCerrar={() => setMovimientosDe(null)} />}
    </div>
  );
}

function DialogoSaldoInicial({ conductor, onCerrar, onGuardado }: { conductor: Conductor; onCerrar: () => void; onGuardado: () => void }) {
  const [magnitud, setMagnitud] = useState(0);
  const [direccion, setDireccion] = useState<'EmpresaDebe' | 'ConductorDebe'>('EmpresaDebe');
  const [fechaCorte, setFechaCorte] = useState(hoyLocalISO());
  const [justificacion, setJustificacion] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutacion = useMutation({
    mutationFn: () =>
      registrarSaldoInicial(conductor.id, {
        valor: direccion === 'EmpresaDebe' ? magnitud : -magnitud,
        fechaCorte,
        justificacion: justificacion.trim(),
      }),
    onSuccess: onGuardado,
    onError: (e) => setError(textoError(e)),
  });

  return (
    <div className="bg-white border border-blue-200 rounded-xl p-5 space-y-4" data-testid="dialogo-saldo-inicial">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-slate-900">Saldo inicial de {conductor.nombreCompleto}</h3>
        <button onClick={onCerrar} className="text-slate-400 hover:text-slate-600 text-sm">
          Cerrar
        </button>
      </div>
      {error && (
        <Alert variant="error" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}
      <div className="grid gap-3 sm:grid-cols-2">
        <MoneyInput label="Valor" value={magnitud} onChange={setMagnitud} />
        <label className="flex flex-col text-xs font-bold text-slate-600 uppercase tracking-wider">
          Dirección
          <select
            value={direccion}
            onChange={(e) => setDireccion(e.target.value as typeof direccion)}
            className="mt-1.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm"
            data-testid="direccion-saldo-inicial"
          >
            <option value="EmpresaDebe">La empresa le debe</option>
            <option value="ConductorDebe">El conductor debe</option>
          </select>
        </label>
        <Input
          label="Fecha de corte"
          type="date"
          value={fechaCorte}
          onChange={(e) => setFechaCorte(e.target.value)}
        />
        <Input
          label="Justificación"
          value={justificacion}
          onChange={(e) => setJustificacion(e.target.value)}
          maxLength={300}
          required
        />
      </div>
      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={onCerrar}>
          Cancelar
        </Button>
        <Button isLoading={mutacion.isPending} disabled={!justificacion.trim()} onClick={() => mutacion.mutate()}>
          Guardar saldo inicial
        </Button>
      </div>
    </div>
  );
}

function PanelMovimientos({ conductor, onCerrar }: { conductor: Conductor; onCerrar: () => void }) {
  const { data, isLoading } = useQuery({
    queryKey: ['conductores', conductor.id, 'movimientos'],
    queryFn: () => obtenerMovimientos(conductor.id),
  });

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-slate-900">Movimientos de {conductor.nombreCompleto}</h3>
        <button onClick={onCerrar} className="text-slate-400 hover:text-slate-600 text-sm">
          Cerrar
        </button>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full text-xs">
          <thead className="bg-slate-50 text-left uppercase text-slate-500">
            <tr>
              <th className="px-3 py-2">Fecha de corte</th>
              <th className="px-3 py-2">Tipo</th>
              <th className="px-3 py-2">Valor</th>
              <th className="px-3 py-2">Saldo resultante</th>
              <th className="px-3 py-2">Justificación</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-3 py-4 text-center text-slate-500">
                  Cargando…
                </td>
              </tr>
            )}
            {data?.items.map((m) => (
              <tr key={m.id}>
                <td className="px-3 py-2 whitespace-nowrap">{m.fechaCorte}</td>
                <td className="px-3 py-2">{m.tipo}</td>
                <td className="px-3 py-2 font-mono">{describirSaldo(m.valor)}</td>
                <td className="px-3 py-2 font-mono">{describirSaldo(m.saldoResultante)}</td>
                <td className="px-3 py-2">{m.justificacion ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
