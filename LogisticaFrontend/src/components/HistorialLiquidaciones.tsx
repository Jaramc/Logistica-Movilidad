import React, { useEffect, useState } from 'react';
import { RefreshCw, FileText, CheckCircle2, Truck } from 'lucide-react';
import type { LiquidacionViaje } from '../types/liquidacion';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5098';

export const HistorialLiquidaciones: React.FC = () => {
  const [liquidaciones, setLiquidaciones] = useState<LiquidacionViaje[]>([]);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cargarDatos = async () => {
    setCargando(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE_URL}/api/liquidaciones`);
      if (!res.ok) throw new Error('Error al consultar liquidaciones');
      const data = await res.json();
      setLiquidaciones(data);
    } catch (err: any) {
      console.error(err);
      setError('No se pudo conectar con el backend. Asegúrate de que LogisticaBackend esté en ejecución.');
    } finally {
      setCargando(false);
    }
  };

  useEffect(() => {
    cargarDatos();
  }, []);

  const formatCOP = (valor: number) => {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(valor || 0);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex items-center justify-between pb-6 border-b border-slate-200 mb-8">
        <div>
          <h2 className="text-2xl font-bold text-slate-900 tracking-tight">Historial de Liquidaciones</h2>
          <p className="text-slate-500 text-sm mt-1">Registros aprobados y persistidos en PostgreSQL.</p>
        </div>
        <button
          onClick={cargarDatos}
          disabled={cargando}
          className="flex items-center gap-2 px-4 py-2 bg-white border border-slate-300 hover:bg-slate-50 rounded-xl text-sm font-semibold text-slate-700 transition"
        >
          <RefreshCw className={`w-4 h-4 ${cargando ? 'animate-spin' : ''}`} />
          Refrescar
        </button>
      </div>

      {error && (
        <div className="p-4 bg-amber-50 border border-amber-200 text-amber-800 rounded-xl mb-6 text-sm">
          {error}
        </div>
      )}

      {liquidaciones.length === 0 && !cargando ? (
        <div className="text-center py-16 bg-white border border-slate-200 rounded-2xl">
          <FileText className="w-12 h-12 text-slate-400 mx-auto mb-3 opacity-50" />
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
                  <th className="py-3.5 px-4">ID</th>
                  <th className="py-3.5 px-4">Vehículo / Placa</th>
                  <th className="py-3.5 px-4">Conductor</th>
                  <th className="py-3.5 px-4">Ruta (Origen - Destino)</th>
                  <th className="py-3.5 px-4 text-right">Total Gastos</th>
                  <th className="py-3.5 px-4 text-right">Saldo Viaje</th>
                  <th className="py-3.5 px-4 text-center">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {liquidaciones.map((liq) => (
                  <tr key={liq.id || Math.random()} className="hover:bg-slate-50/70 transition">
                    <td className="py-3 px-4 font-mono font-medium text-slate-900">#{liq.id}</td>
                    <td className="py-3 px-4 font-mono font-bold text-slate-900 flex items-center gap-1.5">
                      <Truck className="w-4 h-4 text-blue-600" />
                      {liq.placa}
                    </td>
                    <td className="py-3 px-4 text-slate-800">{liq.conductor}</td>
                    <td className="py-3 px-4 text-slate-600">
                      {liq.origenViaje || 'N/A'} → {liq.destino || 'N/A'}
                    </td>
                    <td className="py-3 px-4 text-right font-mono font-medium text-slate-900">
                      {formatCOP(liq.totalGastos)}
                    </td>
                    <td className="py-3 px-4 text-right font-mono font-bold text-blue-700">
                      {formatCOP(liq.saldoViaje)}
                    </td>
                    <td className="py-3 px-4 text-center">
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                        <CheckCircle2 className="w-3 h-3" />
                        {liq.estadoRevision || 'Aprobado'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
};
