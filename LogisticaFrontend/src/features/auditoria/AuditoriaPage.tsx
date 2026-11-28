import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ACCIONES, obtenerAuditoria } from './auditoriaApi';
import { ApiError } from '../../api/types';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';

/** Consulta de solo lectura: no hay forma de editar ni borrar registros. */
export function AuditoriaPage() {
  const [pagina, setPagina] = useState(1);
  const [accion, setAccion] = useState('');
  const { data, isLoading, error, isFetching } = useQuery({
    queryKey: ['auditoria', pagina, accion],
    queryFn: () => obtenerAuditoria(pagina, accion),
  });

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-2xl font-bold text-slate-900">Auditoría</h2>
          <p className="text-sm text-slate-500">Registro de solo lectura de accesos y operaciones sensibles.</p>
        </div>
        <label className="flex flex-col text-xs font-medium text-slate-600">
          Acción
          <select
            value={accion}
            onChange={(e) => {
              setAccion(e.target.value);
              setPagina(1);
            }}
            className="mt-1 rounded-md border border-slate-300 px-2 py-1 text-sm"
          >
            <option value="">Todas</option>
            {ACCIONES.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && <Alert variant="error">{error instanceof ApiError ? error.getMensajeUsuario() : 'No se pudo consultar la auditoría.'}</Alert>}

      <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto">
        <table className="min-w-full text-xs">
          <thead className="bg-slate-50 text-left uppercase text-slate-500">
            <tr>
              <th className="px-3 py-2">Fecha</th>
              <th className="px-3 py-2">Usuario</th>
              <th className="px-3 py-2">Acción</th>
              <th className="px-3 py-2">Entidad</th>
              <th className="px-3 py-2">IP</th>
              <th className="px-3 py-2">Resultado</th>
              <th className="px-3 py-2">Detalle</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={7} className="px-3 py-6 text-center text-slate-500">
                  Cargando…
                </td>
              </tr>
            )}
            {data?.items.map((r) => (
              <tr key={r.id}>
                <td className="px-3 py-2 whitespace-nowrap">{new Date(r.fechaUtc).toLocaleString('es-CO')}</td>
                <td className="px-3 py-2">{r.usuarioEmail ?? r.usuarioId ?? '—'}</td>
                <td className="px-3 py-2 font-medium">{r.accion}</td>
                <td className="px-3 py-2">{r.entidad ? `${r.entidad} ${r.entidadId ?? ''}` : '—'}</td>
                <td className="px-3 py-2">{r.ip ?? '—'}</td>
                <td className={`px-3 py-2 ${r.exito ? 'text-emerald-700' : 'text-rose-700'}`}>{r.exito ? 'Éxito' : 'Fallo'}</td>
                {/* Texto plano: React escapa el contenido, nunca se interpreta como HTML. */}
                <td className="px-3 py-2 font-mono text-[11px] text-slate-600 max-w-xs truncate" title={r.detalle ?? ''}>
                  {r.detalle ?? ''}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-end gap-2 text-xs text-slate-600">
        <span>
          Página {data?.page ?? pagina} de {Math.max(1, data?.totalPages ?? 1)} · {data?.totalCount ?? 0} registros
        </span>
        <Button size="sm" variant="outline" disabled={pagina <= 1 || isFetching} onClick={() => setPagina((p) => p - 1)}>
          Anterior
        </Button>
        <Button
          size="sm"
          variant="outline"
          disabled={pagina >= (data?.totalPages ?? 1) || isFetching}
          onClick={() => setPagina((p) => p + 1)}
        >
          Siguiente
        </Button>
      </div>
    </div>
  );
}
