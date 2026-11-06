import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw, FileText, CheckCircle2, Truck } from 'lucide-react';
import { obtenerHistorialApi } from '../api/planillasApi';
import { formatCOP } from '../features/planillas/utils/money';
import DescargarReporte from './DescargarReporte';
import { Button } from './ui/Button';
import { Alert } from './ui/Alert';

interface FilaHistorial {
  id?: number;
  placa?: string;
  conductor?: string;
  origenViaje?: string;
  origen?: string;
  destino?: string;
  totalGastos?: number;
  saldoViaje?: number;
  estadoRevision?: string;
}

export const HistorialLiquidaciones: React.FC = () => {
  const { data, isLoading, error, refetch, isFetching } = useQuery<FilaHistorial[]>({
    queryKey: ['historial-liquidaciones'],
    queryFn: async () => {
      const res = await obtenerHistorialApi();
      return (res ?? []) as FilaHistorial[];
    },
    staleTime: 30000,
  });

  const liquidaciones = data ?? [];

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-6 border-b border-slate-200 mb-8 gap-4">
        <div>
          <h2 className="text-2xl font-bold text-slate-900 tracking-tight">Historial de Liquidaciones</h2>
          <p className="text-slate-500 text-sm mt-1">Registros aprobados y persistidos en PostgreSQL.</p>
        </div>

        {/* Acciones del encabezado: Refrescar y Descargar Consolidado Excel */}
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isFetching}
            leftIcon={<RefreshCw className={`w-4 h-4 ${isFetching ? 'animate-spin' : ''}`} />}
          >
            Refrescar
          </Button>

          <DescargarReporte />
        </div>
      </div>

      {error && (
        <Alert variant="warning" title="Aviso de Conexión" className="mb-6">
          No se pudo consultar el historial en PostgreSQL. Asegúrate de que el backend esté en ejecución con la base de datos conectada.
        </Alert>
      )}

      {liquidaciones.length === 0 && !isLoading ? (
        <div className="text-center py-16 bg-white border border-slate-200 rounded-2xl">
          <FileText className="w-12 h-12 text-slate-400 mx-auto mb-3 opacity-50" aria-hidden="true" />
          <p className="text-slate-700 font-semibold text-base">Aún no hay liquidaciones registradas</p>
          <p className="text-slate-500 text-sm mt-1">
            Sube un comprobante en la pestaña "Digitalizar Liquidación" y presiona "Aprobar y Guardar".
          </p>
        </div>
      ) : (
        <div className="bg-white border border-slate-200 rounded-2xl overflow-hidden shadow-xs">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="bg-slate-50 border-b border-slate-200 text-xs uppercase font-semibold text-slate-500">
                <tr>
                  <th scope="col" className="py-3.5 px-4">ID</th>
                  <th scope="col" className="py-3.5 px-4">Vehículo / Placa</th>
                  <th scope="col" className="py-3.5 px-4">Conductor</th>
                  <th scope="col" className="py-3.5 px-4">Ruta (Origen - Destino)</th>
                  <th scope="col" className="py-3.5 px-4 text-right">Total Gastos</th>
                  <th scope="col" className="py-3.5 px-4 text-right">Saldo Viaje</th>
                  <th scope="col" className="py-3.5 px-4 text-center">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {liquidaciones.map((liq, index) => {
                  const claveUnica = liq.id ? `liq_${liq.id}` : `fila_${index}`;
                  return (
                    <tr key={claveUnica} className="hover:bg-slate-50/70 transition">
                      <td className="py-3 px-4 font-mono font-medium text-slate-900">#{liq.id ?? index + 1}</td>
                      <td className="py-3 px-4 font-mono font-bold text-slate-900 flex items-center gap-1.5">
                        <Truck className="w-4 h-4 text-blue-600" aria-hidden="true" />
                        {liq.placa || 'N/A'}
                      </td>
                      <td className="py-3 px-4 text-slate-800">{liq.conductor || 'N/A'}</td>
                      <td className="py-3 px-4 text-slate-600">
                        {liq.origen || liq.origenViaje || 'N/A'} → {liq.destino || 'N/A'}
                      </td>
                      <td className="py-3 px-4 text-right font-mono font-medium text-slate-900">
                        {formatCOP(liq.totalGastos ?? 0)}
                      </td>
                      <td className="py-3 px-4 text-right font-mono font-bold text-blue-700">
                        {formatCOP(liq.saldoViaje ?? 0)}
                      </td>
                      <td className="py-3 px-4 text-center">
                        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                          <CheckCircle2 className="w-3 h-3" aria-hidden="true" />
                          {liq.estadoRevision || 'Aprobado'}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
