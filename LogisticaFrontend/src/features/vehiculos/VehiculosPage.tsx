import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../../api/types';
import { Alert } from '../../components/ui/Alert';
import { Button } from '../../components/ui/Button';
import { Input } from '../../components/ui/Input';
import { crearVehiculo, desactivarVehiculo, listarVehiculos } from './vehiculosApi';

const CLAVE = ['vehiculos'];

function textoError(error: unknown): string {
  if (error instanceof ApiError) {
    const campos = error.fieldErrors ? Object.values(error.fieldErrors).flat().join(' ') : '';
    return campos || error.getMensajeUsuario();
  }
  return 'No se pudo completar la operación.';
}

export function VehiculosPage() {
  const queryClient = useQueryClient();
  const { data: vehiculos = [], isLoading, error } = useQuery({ queryKey: CLAVE, queryFn: () => listarVehiculos() });

  const [placa, setPlaca] = useState('');
  const [errorAccion, setErrorAccion] = useState<string | null>(null);

  const refrescar = () => queryClient.invalidateQueries({ queryKey: CLAVE });
  const alFallar = (e: unknown) => setErrorAccion(textoError(e));

  const crear = useMutation({
    mutationFn: crearVehiculo,
    onSuccess: () => {
      setPlaca('');
      void refrescar();
    },
    onError: alFallar,
  });
  const desactivar = useMutation({ mutationFn: desactivarVehiculo, onSuccess: refrescar, onError: alFallar });

  const enviar = (e: FormEvent) => {
    e.preventDefault();
    setErrorAccion(null);
    crear.mutate({ placa: placa.trim() });
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-slate-900">Vehículos</h2>
        <p className="text-sm text-slate-500">Nunca se borra un vehículo: solo se desactiva.</p>
      </div>

      {errorAccion && (
        <Alert variant="error" onDismiss={() => setErrorAccion(null)}>
          {errorAccion}
        </Alert>
      )}
      {error && <Alert variant="error">{textoError(error)}</Alert>}

      <form onSubmit={enviar} className="bg-white border border-slate-200 rounded-xl p-4 grid gap-3 sm:grid-cols-3 items-end">
        <Input
          label="Placa"
          value={placa}
          onChange={(e) => setPlaca(e.target.value.toUpperCase())}
          placeholder="Ej. STW514"
          maxLength={10}
          required
        />
        <Button type="submit" isLoading={crear.isPending} disabled={!placa}>
          Crear vehículo
        </Button>
      </form>

      <div className="bg-white border border-slate-200 rounded-xl overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-2">Placa</th>
              <th className="px-4 py-2">Tipo</th>
              <th className="px-4 py-2">Estado</th>
              <th className="px-4 py-2">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-slate-500">
                  Cargando…
                </td>
              </tr>
            )}
            {vehiculos.map((v) => (
              <tr key={v.id}>
                <td className="px-4 py-2 font-mono">{v.placa}</td>
                <td className="px-4 py-2">{v.tipo}</td>
                <td className="px-4 py-2 text-xs">{v.activo ? 'Activo' : 'Inactivo'}</td>
                <td className="px-4 py-2">
                  <Button
                    size="sm"
                    variant={v.activo ? 'danger' : 'success'}
                    onClick={() => {
                      if (window.confirm(`¿${v.activo ? 'Desactivar' : 'Activar'} el vehículo ${v.placa}?`)) {
                        desactivar.mutate(v.id);
                      }
                    }}
                  >
                    {v.activo ? 'Desactivar' : 'Activar'}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
