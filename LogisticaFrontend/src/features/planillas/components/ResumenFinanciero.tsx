import React from 'react';
import { aFavorDe, formatCOP, type CalculoFinanciero } from '../utils/money';
import Big, { type BigSource } from 'big.js';

export interface ResumenFinancieroProps {
  flete: number | string;
  anticipo: number | string;
  calculo: CalculoFinanciero;
  cantidadGastos: number;
  /** Saldo anterior que asigna el servidor (con signo). */
  saldoAnterior?: BigSource;
}

/** "a favor de: Conductor" / "a favor de: Empresa" según el signo (positivo = la empresa le debe al conductor). */
const AFavor: React.FC<{ saldo: BigSource; className?: string }> = ({ saldo, className }) => {
  const quien = aFavorDe(saldo);
  if (!quien) return <span className={className}>Sin saldo pendiente</span>;
  return (
    <span className={className} data-testid="a-favor-de">
      a favor de: <strong>{quien}</strong>
    </span>
  );
};

export const ResumenFinanciero: React.FC<ResumenFinancieroProps> = ({
  flete,
  anticipo,
  calculo,
  cantidadGastos,
  saldoAnterior = 0,
}) => {
  const fleteBig = new Big(flete || 0);
  const anticipoBig = new Big(anticipo || 0);
  const esSaldoAFavorConductor = calculo.saldoViaje.gte(0);

  return (
    <div className="w-full bg-white rounded-2xl border border-slate-200/90 shadow-2xs p-4 sm:p-5">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-3 mb-4 border-b border-slate-100 gap-2">
        <div>
          <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">
            Balance Económico del Viaje
          </h4>
          <p className="text-xs text-slate-500 mt-0.5">
            Cálculo exacto en tiempo real sobre {cantidadGastos} rubro{cantidadGastos === 1 ? '' : 's'} de gasto.
          </p>
        </div>

        {/* Indicador Semántico Accesible (no solo color) */}
        <span
          className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold ${
            esSaldoAFavorConductor
              ? 'bg-blue-50 text-blue-700 border border-blue-200'
              : 'bg-amber-50 text-amber-800 border border-amber-200'
          }`}
        >
          <span
            className={`w-2 h-2 rounded-full ${esSaldoAFavorConductor ? 'bg-blue-600' : 'bg-amber-600'}`}
            aria-hidden="true"
          />
          {esSaldoAFavorConductor ? 'Saldo a Favor del Conductor (Reembolso)' : 'Reintegro a la Empresa (Sobrante)'}
        </span>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
        {/* 1. Flete */}
        <div className="p-3 bg-slate-50/70 rounded-xl border border-slate-100">
          <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wide">Flete Acordado</p>
          <p className="text-base font-bold text-slate-800 font-mono mt-1">
            {formatCOP(fleteBig)}
          </p>
        </div>

        {/* 2. Anticipo */}
        <div className="p-3 bg-slate-50/70 rounded-xl border border-slate-100">
          <p className="text-[11px] font-semibold text-indigo-500 uppercase tracking-wide">Anticipo Entregado</p>
          <p className="text-base font-bold text-indigo-700 font-mono mt-1">
            {formatCOP(anticipoBig)}
          </p>
        </div>

        {/* 3. Total Gastos */}
        <div className="p-3 bg-slate-50/70 rounded-xl border border-slate-100">
          <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wide">Total Gastos ({cantidadGastos})</p>
          <p className="text-base font-bold text-slate-900 font-mono mt-1">
            {formatCOP(calculo.totalGastos)}
          </p>
        </div>

        {/* 4. Saldo Viaje */}
        <div
          className={`p-3 rounded-xl border ${
            esSaldoAFavorConductor
              ? 'bg-blue-50/80 border-blue-200 text-blue-900'
              : 'bg-rose-50/80 border-rose-200 text-rose-900'
          }`}
        >
          <p className="text-[11px] font-semibold uppercase tracking-wide opacity-80">
            Saldo del Viaje
          </p>
          <p className="text-base font-bold font-mono mt-1">
            {formatCOP(calculo.saldoViaje.abs())}
          </p>
          <AFavor saldo={calculo.saldoViaje} className="text-[10px] block mt-0.5" />
          <span className="text-[10px] opacity-75 block">Gastos − Anticipo</span>
        </div>

        {/* 5. Saldo Total Definitivo */}
        <div className="p-3 bg-emerald-50/80 rounded-xl border border-emerald-200 col-span-2 lg:col-span-1">
          <p className="text-[11px] font-semibold text-emerald-800 uppercase tracking-wide">Saldo Total</p>
          <p className="text-base font-bold text-emerald-900 font-mono mt-1">
            {formatCOP(calculo.saldoTotal.abs())}
          </p>
          <AFavor saldo={calculo.saldoTotal} className="text-[10px] text-emerald-800 block mt-0.5" />
          <span className="text-[10px] text-emerald-700 block" data-testid="saldo-anterior-servidor">
            Saldo Viaje + Anterior ({formatCOP(new Big(saldoAnterior || 0).abs())}
            {aFavorDe(saldoAnterior) ? ` a favor de: ${aFavorDe(saldoAnterior)}` : ''})
          </span>
        </div>
      </div>
    </div>
  );
};
