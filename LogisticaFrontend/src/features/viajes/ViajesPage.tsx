import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/types';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { Input } from '../../components/ui/Input';
import { MoneyInput } from '../../components/ui/MoneyInput';
import ProcesadorLiquidaciones from '../../components/ProcesadorLiquidaciones';
import { useSesion } from '../auth/sesionContext';
import { describirSaldo, formatCOP } from '../planillas/utils/money';
import { hoyLocalISO } from '../../lib/fechas';
import { listarConductores } from '../conductores/conductoresApi';
import { registrarPago } from '../conductores/conductoresApi';
import { listarVehiculos } from '../vehiculos/vehiculosApi';
import {
  abrirViaje,
  aprobarViaje,
  listarViajes,
  obtenerViaje,
  reabrirViaje,
  registrarTraslado,
  type EstadoViaje,
  type MedioTraslado,
} from './viajesApi';

const CLAVE = (filtros: { pagina: number; estado: string }) => ['viajes', filtros.pagina, filtros.estado];
const ESTADOS: EstadoViaje[] = ['Abierto', 'EnRevision', 'Aprobado', 'Saldado'];
const MEDIOS: MedioTraslado[] = ['Transferencia', 'Efectivo', 'Giro'];

function textoError(error: unknown): string {
  if (error instanceof ApiError) {
    const campos = error.fieldErrors ? Object.values(error.fieldErrors).flat().join(' ') : '';
    return campos || error.getMensajeUsuario();
  }
  return 'No se pudo completar la operación.';
}

export function ViajesPage() {
  const { usuario } = useSesion();
  const puedeEscribir = usuario?.roles.some((r) => r === 'Administrador' || r === 'Revisor') ?? false;
  const esAdministrador = usuario?.roles.includes('Administrador') ?? false;

  const [pagina, setPagina] = useState(1);
  const [estado, setEstado] = useState('');
  const [viajeId, setViajeId] = useState<number | null>(null);
  const [procesandoPlanillaDe, setProcesandoPlanillaDe] = useState<number | null>(null);
  const [mostrarAbrir, setMostrarAbrir] = useState(false);

  const queryClient = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: CLAVE({ pagina, estado }),
    queryFn: () => listarViajes(pagina, 20, { estado: estado as EstadoViaje | '' }),
  });

  const refrescarLista = () => queryClient.invalidateQueries({ queryKey: ['viajes'] });

  if (procesandoPlanillaDe !== null) {
    return (
      <div className="max-w-[1600px] mx-auto px-4 sm:px-6 pt-4">
        <Button variant="outline" size="sm" onClick={() => setProcesandoPlanillaDe(null)} className="mb-2">
          ← Volver a Viajes
        </Button>
        <ProcesadorLiquidaciones
          viajeId={procesandoPlanillaDe}
          onGuardado={() => {
            setProcesandoPlanillaDe(null);
            void refrescarLista();
          }}
        />
      </div>
    );
  }

  if (viajeId !== null) {
    return (
      <DetalleViaje
        viajeId={viajeId}
        puedeEscribir={puedeEscribir}
        esAdministrador={esAdministrador}
        onVolver={() => setViajeId(null)}
        onCargarPlanilla={(id) => setProcesandoPlanillaDe(id)}
        onCambio={refrescarLista}
      />
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Viajes</h2>
          <p className="text-sm text-slate-500">Un viaje por conductor y por vehículo mientras esté abierto o en revisión.</p>
        </div>
        <div className="flex items-end gap-3">
          <label className="flex flex-col text-xs font-medium text-slate-600">
            Estado
            <select
              value={estado}
              onChange={(e) => {
                setEstado(e.target.value);
                setPagina(1);
              }}
              className="mt-1 rounded-md border border-slate-300 px-2 py-1 text-sm"
            >
              <option value="">Todos</option>
              {ESTADOS.map((e) => (
                <option key={e} value={e}>
                  {e}
                </option>
              ))}
            </select>
          </label>
          {puedeEscribir && <Button onClick={() => setMostrarAbrir((v) => !v)}>Abrir viaje</Button>}
        </div>
      </div>

      {error && <Alert variant="error">{textoError(error)}</Alert>}

      {mostrarAbrir && <FormularioAbrirViaje onCerrar={() => setMostrarAbrir(false)} onCreado={() => { setMostrarAbrir(false); void refrescarLista(); }} />}

      <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-2">Conductor</th>
              <th className="px-4 py-2">Placa</th>
              <th className="px-4 py-2">Cliente</th>
              <th className="px-4 py-2">Fecha inicio</th>
              <th className="px-4 py-2">Estado</th>
              <th className="px-4 py-2">Saldo del viaje</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-slate-500">
                  Cargando…
                </td>
              </tr>
            )}
            {data?.items.map((v) => (
              <tr key={v.id}>
                <td className="px-4 py-2">
                  {v.conductorCodigo} · {v.conductorNombre}
                </td>
                <td className="px-4 py-2 font-mono">{v.placa}</td>
                <td className="px-4 py-2">{v.cliente}</td>
                <td className="px-4 py-2 whitespace-nowrap">{v.fechaInicio}</td>
                <td className="px-4 py-2 text-xs">{v.estado}</td>
                <td className="px-4 py-2 font-mono">{v.saldoViaje !== null ? describirSaldo(v.saldoViaje) : '—'}</td>
                <td className="px-4 py-2">
                  <Button size="sm" variant="outline" onClick={() => setViajeId(v.id)}>
                    Ver
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-end gap-2 text-xs text-slate-600">
        <span>
          Página {data?.page ?? pagina} de {Math.max(1, data?.totalPages ?? 1)} · {data?.totalCount ?? 0} viajes
        </span>
        <Button size="sm" variant="outline" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </Button>
        <Button size="sm" variant="outline" disabled={pagina >= (data?.totalPages ?? 1)} onClick={() => setPagina((p) => p + 1)}>
          Siguiente
        </Button>
      </div>
    </div>
  );
}

function FormularioAbrirViaje({ onCerrar, onCreado }: { onCerrar: () => void; onCreado: () => void }) {
  const { data: conductores = [] } = useQuery({ queryKey: ['conductores', 'activos'], queryFn: () => listarConductores(true) });
  const { data: vehiculos = [] } = useQuery({ queryKey: ['vehiculos', 'activos'], queryFn: () => listarVehiculos(true) });

  const [conductorId, setConductorId] = useState('');
  const [vehiculoId, setVehiculoId] = useState('');
  const [origen, setOrigen] = useState('');
  const [destino, setDestino] = useState('');
  const [cliente, setCliente] = useState('');
  const [manifiesto, setManifiesto] = useState('');
  const [flete, setFlete] = useState(0);
  const [fechaInicio, setFechaInicio] = useState(hoyLocalISO());
  const [error, setError] = useState<string | null>(null);

  const mutacion = useMutation({
    mutationFn: () =>
      abrirViaje({
        conductorId: Number(conductorId),
        vehiculoId: Number(vehiculoId),
        origen,
        destino,
        cliente,
        manifiesto: manifiesto.trim() || null,
        flete,
        fechaInicio,
      }),
    onSuccess: onCreado,
    onError: (e) => setError(textoError(e)),
  });

  const enviar = (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    mutacion.mutate();
  };

  return (
    <form onSubmit={enviar} className="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-bold text-slate-900">Abrir viaje</h3>
        <button type="button" onClick={onCerrar} className="text-slate-400 hover:text-slate-600 text-sm">
          Cerrar
        </button>
      </div>
      {error && (
        <Alert variant="error" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}
      <div className="grid gap-3 sm:grid-cols-3">
        <label className="flex flex-col text-xs font-bold text-slate-600 uppercase tracking-wider">
          Conductor
          <select
            value={conductorId}
            onChange={(e) => setConductorId(e.target.value)}
            className="mt-1.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm"
            required
          >
            <option value="">Seleccione…</option>
            {conductores.map((c) => (
              <option key={c.id} value={c.id}>
                {c.codigo} · {c.nombreCompleto}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col text-xs font-bold text-slate-600 uppercase tracking-wider">
          Vehículo
          <select
            value={vehiculoId}
            onChange={(e) => setVehiculoId(e.target.value)}
            className="mt-1.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm"
            required
          >
            <option value="">Seleccione…</option>
            {vehiculos.map((v) => (
              <option key={v.id} value={v.id}>
                {v.placa}
              </option>
            ))}
          </select>
        </label>
        <Input label="Cliente" value={cliente} onChange={(e) => setCliente(e.target.value)} required />
        <Input label="Origen" value={origen} onChange={(e) => setOrigen(e.target.value)} required />
        <Input label="Destino" value={destino} onChange={(e) => setDestino(e.target.value)} required />
        <Input label="Manifiesto" value={manifiesto} onChange={(e) => setManifiesto(e.target.value)} />
        <MoneyInput label="Flete" value={flete} onChange={setFlete} />
        <Input label="Fecha de inicio" type="date" value={fechaInicio} onChange={(e) => setFechaInicio(e.target.value)} required />
      </div>
      <div className="flex justify-end gap-2">
        <Button type="submit" isLoading={mutacion.isPending}>
          Abrir viaje
        </Button>
      </div>
    </form>
  );
}

interface DetalleViajeProps {
  viajeId: number;
  puedeEscribir: boolean;
  esAdministrador: boolean;
  onVolver: () => void;
  onCargarPlanilla: (viajeId: number) => void;
  onCambio: () => void;
}

function DetalleViaje({ viajeId, puedeEscribir, esAdministrador, onVolver, onCargarPlanilla, onCambio }: DetalleViajeProps) {
  const queryClient = useQueryClient();
  const clave = ['viajes', 'detalle', viajeId];
  const { data: viaje, isLoading, error } = useQuery({ queryKey: clave, queryFn: () => obtenerViaje(viajeId) });
  const [mostrarTraslado, setMostrarTraslado] = useState(false);
  const [mostrarPago, setMostrarPago] = useState(false);
  const [errorAccion, setErrorAccion] = useState<string | null>(null);
  const [advertenciasAprobar, setAdvertenciasAprobar] = useState<string[] | null>(null);

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: clave });
    onCambio();
  };

  const aprobar = useMutation({
    mutationFn: () => aprobarViaje(viajeId),
    onSuccess: (r) => {
      setAdvertenciasAprobar(r.advertencias?.length ? r.advertencias : null);
      refrescar();
    },
    onError: (e) => setErrorAccion(textoError(e)),
  });

  const reabrir = useMutation({
    mutationFn: (motivo: string) => reabrirViaje(viajeId, motivo),
    onSuccess: refrescar,
    onError: (e) => setErrorAccion(textoError(e)),
  });

  if (isLoading) return <p className="p-8 text-center text-slate-500">Cargando…</p>;
  if (error || !viaje) return <Alert variant="error">{textoError(error)}</Alert>;

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      <div className="flex items-center justify-between">
        <Button variant="outline" size="sm" onClick={onVolver}>
          ← Volver
        </Button>
        <span className="text-xs font-semibold uppercase text-slate-500">{viaje.estado}</span>
      </div>

      {errorAccion && (
        <Alert variant="error" onDismiss={() => setErrorAccion(null)}>
          {errorAccion}
        </Alert>
      )}
      {advertenciasAprobar && (
        <Alert variant="warning" title="Revisa estas diferencias" onDismiss={() => setAdvertenciasAprobar(null)}>
          <ul className="list-disc pl-4 space-y-1">
            {advertenciasAprobar.map((a) => (
              <li key={a}>{a}</li>
            ))}
          </ul>
        </Alert>
      )}

      <div className="bg-white border border-slate-200 rounded-xl p-5 grid gap-3 sm:grid-cols-2">
        <div>
          <p className="text-xs uppercase text-slate-500">Conductor</p>
          <p className="font-semibold">{viaje.conductorCodigo} · {viaje.conductorNombre}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Placa</p>
          <p className="font-semibold font-mono">{viaje.placa}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Ruta</p>
          <p className="font-semibold">{viaje.origen} → {viaje.destino}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Cliente</p>
          <p className="font-semibold">{viaje.cliente}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Flete</p>
          <p className="font-semibold font-mono">{formatCOP(viaje.flete)}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Anticipo (traslados)</p>
          <p className="font-semibold font-mono">{formatCOP(viaje.anticipoTraslados)}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Saldo del viaje</p>
          <p className="font-semibold font-mono">{viaje.saldoViaje !== null ? describirSaldo(viaje.saldoViaje) : 'Sin aprobar'}</p>
        </div>
        <div>
          <p className="text-xs uppercase text-slate-500">Pendiente</p>
          <p className="font-semibold font-mono">{viaje.pendiente !== null ? describirSaldo(viaje.pendiente) : '—'}</p>
        </div>
      </div>

      <div className="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="font-bold text-slate-900">Traslados</h3>
          {puedeEscribir && (viaje.estado === 'Abierto' || viaje.estado === 'EnRevision') && (
            <Button size="sm" onClick={() => setMostrarTraslado((v) => !v)}>
              Registrar traslado
            </Button>
          )}
        </div>
        {mostrarTraslado && (
          <FormularioTraslado
            viajeId={viajeId}
            onCreado={() => {
              setMostrarTraslado(false);
              refrescar();
            }}
          />
        )}
        <table className="min-w-full text-xs">
          <thead className="bg-slate-50 text-left uppercase text-slate-500">
            <tr>
              <th className="px-3 py-2">Fecha</th>
              <th className="px-3 py-2">Valor</th>
              <th className="px-3 py-2">Medio</th>
              <th className="px-3 py-2">Nota</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {viaje.traslados.length === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-4 text-center text-slate-500">
                  Sin traslados registrados.
                </td>
              </tr>
            )}
            {viaje.traslados.map((t) => (
              <tr key={t.id}>
                <td className="px-3 py-2 whitespace-nowrap">{t.fecha}</td>
                <td className="px-3 py-2 font-mono">{formatCOP(t.valor)}</td>
                <td className="px-3 py-2">{t.medio}</td>
                <td className="px-3 py-2">{t.nota ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="bg-white border border-slate-200 rounded-xl p-5 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="font-bold text-slate-900">Planilla</h3>
          {puedeEscribir && (viaje.estado === 'Abierto' || viaje.estado === 'EnRevision') && viaje.planillaId === null && (
            <Button size="sm" onClick={() => onCargarPlanilla(viajeId)}>
              Cargar planilla
            </Button>
          )}
        </div>
        <p className="text-sm text-slate-600">
          {viaje.planillaId !== null ? `Planilla #${viaje.planillaId} guardada.` : 'Este viaje todavía no tiene una planilla guardada.'}
        </p>
      </div>

      <div className="flex flex-wrap gap-2 items-center">
        {puedeEscribir && (viaje.estado === 'Abierto' || viaje.estado === 'EnRevision') && (
          <Button
            variant="success"
            isLoading={aprobar.isPending}
            onClick={() => aprobar.mutate()}
          >
            Aprobar viaje
          </Button>
        )}
        {puedeEscribir && (viaje.estado === 'Aprobado' || viaje.estado === 'Saldado') && (
          <Button variant="secondary" onClick={() => setMostrarPago((v) => !v)}>
            Registrar pago o descuento
          </Button>
        )}
        {esAdministrador && (viaje.estado === 'Aprobado' || viaje.estado === 'Saldado') && (
          <Button
            variant="outline"
            isLoading={reabrir.isPending}
            onClick={() => {
              const motivo = window.prompt('Motivo de la reapertura:');
              if (motivo && motivo.trim()) reabrir.mutate(motivo.trim());
            }}
          >
            Reabrir viaje
          </Button>
        )}
      </div>

      {mostrarPago && (
        <FormularioPago
          conductorId={viaje.conductorId}
          viajeId={viajeId}
          onCreado={() => {
            setMostrarPago(false);
            refrescar();
          }}
        />
      )}
    </div>
  );
}

function FormularioTraslado({ viajeId, onCreado }: { viajeId: number; onCreado: () => void }) {
  const [fecha, setFecha] = useState(hoyLocalISO());
  const [valor, setValor] = useState(0);
  const [medio, setMedio] = useState<MedioTraslado>('Transferencia');
  const [nota, setNota] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutacion = useMutation({
    mutationFn: () => registrarTraslado(viajeId, { fecha, valor, medio, nota: nota.trim() || null }),
    onSuccess: onCreado,
    onError: (e) => setError(textoError(e)),
  });

  return (
    <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-3">
      {error && (
        <Alert variant="error" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}
      <div className="grid gap-3 sm:grid-cols-4">
        <Input label="Fecha" type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
        <MoneyInput label="Valor" value={valor} onChange={setValor} />
        <label className="flex flex-col text-xs font-bold text-slate-600 uppercase tracking-wider">
          Medio
          <select value={medio} onChange={(e) => setMedio(e.target.value as MedioTraslado)} className="mt-1.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm">
            {MEDIOS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </label>
        <Input label="Nota" value={nota} onChange={(e) => setNota(e.target.value)} />
      </div>
      <div className="flex justify-end">
        <Button size="sm" isLoading={mutacion.isPending} disabled={valor <= 0} onClick={() => mutacion.mutate()}>
          Guardar traslado
        </Button>
      </div>
    </div>
  );
}

function FormularioPago({ conductorId, viajeId, onCreado }: { conductorId: number; viajeId: number; onCreado: () => void }) {
  const [tipo, setTipo] = useState<'PagoEmpresa' | 'DescuentoNomina'>('PagoEmpresa');
  const [valor, setValor] = useState(0);
  const [fecha, setFecha] = useState(hoyLocalISO());
  const [error, setError] = useState<string | null>(null);

  const mutacion = useMutation({
    mutationFn: () => registrarPago(conductorId, { viajeId, tipo, valor, fecha }),
    onSuccess: onCreado,
    onError: (e) => setError(textoError(e)),
  });

  return (
    <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-3">
      <h4 className="font-semibold text-slate-800 text-sm">Registrar pago o descuento</h4>
      {error && (
        <Alert variant="error" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}
      <div className="grid gap-3 sm:grid-cols-3">
        <label className="flex flex-col text-xs font-bold text-slate-600 uppercase tracking-wider">
          Tipo
          <select value={tipo} onChange={(e) => setTipo(e.target.value as typeof tipo)} className="mt-1.5 rounded-xl border border-slate-200 px-3 py-2.5 text-sm">
            <option value="PagoEmpresa">La empresa le paga</option>
            <option value="DescuentoNomina">Se le descuenta de nómina</option>
          </select>
        </label>
        <MoneyInput label="Valor" value={valor} onChange={setValor} />
        <Input label="Fecha" type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} />
      </div>
      <div className="flex justify-end">
        <Button size="sm" isLoading={mutacion.isPending} disabled={valor <= 0} onClick={() => mutacion.mutate()}>
          Guardar
        </Button>
      </div>
    </div>
  );
}
