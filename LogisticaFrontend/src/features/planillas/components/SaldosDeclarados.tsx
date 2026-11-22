import React from 'react';
import Big, { type BigSource } from 'big.js';
import { aFavorDe, formatCOP, type CalculoFinanciero } from '../utils/money';
import type { SaldoDeclarado } from '../schemas/planillaSchema';

export interface SaldosDeclaradosProps {
  totalGastos: number | null;
  saldoViaje: SaldoDeclarado | null;
  saldoAnterior: SaldoDeclarado | null;
  saldoNuevo: SaldoDeclarado | null;
  calculo: CalculoFinanciero;
  /** Saldo anterior que asigna el servidor; null mientras no se conoce el conductor. */
  saldoAnteriorServidor: number | null;
}

interface Fila {
  nombre: string;
  declarado: BigSource | null;
  aFavor: string | null;
  calculado: BigSource | null;
  conSigno: boolean;
}

/**
 * "Saldos declarados en la planilla": lo escrito en papel, de solo lectura, junto a lo que calcula
 * el sistema. Las diferencias se marcan en ámbar; nunca reemplazan el cálculo.
 */
export const SaldosDeclarados: React.FC<SaldosDeclaradosProps> = ({
  totalGastos, saldoViaje, saldoAnterior, saldoNuevo, calculo, saldoAnteriorServidor,
}) => {
  const filas: Fila[] = [
    { nombre: 'Total gastos', declarado: totalGastos, aFavor: null, calculado: calculo.totalGastos, conSigno: false },
    { nombre: 'Saldo viaje', declarado: saldoViaje?.valor ?? null, aFavor: saldoViaje?.aFavorDe ?? null, calculado: calculo.saldoViaje, conSigno: true },
    { nombre: 'Saldo anterior', declarado: saldoAnterior?.valor ?? null, aFavor: saldoAnterior?.aFavorDe ?? null, calculado: saldoAnteriorServidor, conSigno: true },
    { nombre: 'Saldo nuevo', declarado: saldoNuevo?.valor ?? null, aFavor: saldoNuevo?.aFavorDe ?? null, calculado: calculo.saldoTotal, conSigno: true },
  ];

  if (filas.every((f) => f.declarado === null)) return null;

  return (
    <section
      className="w-full bg-white rounded-2xl border border-slate-200/90 shadow-2xs p-4 sm:p-5"
      aria-labelledby="titulo-saldos-declarados"
      data-testid="saldos-declarados"
    >
      <h4 id="titulo-saldos-declarados" className="text-xs font-bold text-slate-400 uppercase tracking-wider mb-3">
        Saldos declarados en la planilla
      </h4>
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {filas.map((f) => {
          const difiere = f.declarado !== null && f.calculado !== null && !new Big(f.declarado).eq(new Big(f.calculado));
          const quien = f.conSigno && f.declarado !== null ? f.aFavor ?? aFavorDe(f.declarado) : null;
          return (
            <div
              key={f.nombre}
              data-testid={`declarado-${f.nombre.toLowerCase().replace(' ', '-')}`}
              data-difiere={difiere || undefined}
              className={`p-3 rounded-xl border ${difiere ? 'bg-amber-50 border-amber-300 text-amber-900' : 'bg-slate-50/70 border-slate-100 text-slate-800'}`}
            >
              <p className="text-[11px] font-semibold uppercase tracking-wide opacity-80">{f.nombre}</p>
              <p className="text-base font-bold font-mono mt-1">
                {f.declarado === null ? '—' : formatCOP(new Big(f.declarado).abs())}
              </p>
              {quien && <span className="text-[10px] block">a favor de: <strong>{quien}</strong></span>}
              {difiere && (
                <span className="text-[10px] block mt-0.5">
                  Sistema: {formatCOP(new Big(f.calculado!).abs())}
                  {f.conSigno && aFavorDe(f.calculado!) ? ` a favor de: ${aFavorDe(f.calculado!)}` : ''}
                </span>
              )}
            </div>
          );
        })}
      </div>
    </section>
  );
};
