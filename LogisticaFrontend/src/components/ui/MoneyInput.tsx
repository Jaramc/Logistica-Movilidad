import React, { useState, forwardRef, useId } from 'react';
import { cn } from '../../lib/utils';
import { parseCOP, formatCOP } from '../../features/planillas/utils/money';

export interface MoneyInputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'> {
  label?: string;
  error?: string;
  helperText?: string;
  value: number | string;
  onChange: (valorNumerico: number) => void;
  permiteNegativos?: boolean;
}

export const MoneyInput = forwardRef<HTMLInputElement, MoneyInputProps>(
  (
    {
      className,
      label,
      error,
      helperText,
      value,
      onChange,
      id,
      disabled,
      required,
      permiteNegativos = false,
      ...props
    },
    ref
  ) => {
    const generatedId = useId();
    const inputId = id || generatedId;
    const errorId = `${inputId}-error`;

    const [estaEnFoco, setEstaEnFoco] = useState(false);
    const [textoLocal, setTextoLocal] = useState('');

    const bigValActual = parseCOP(value);
    const valorFormateado = bigValActual.eq(0) ? '' : formatCOP(bigValActual);
    const valorAMostrar = estaEnFoco ? textoLocal : valorFormateado;

    const handleFocus = (e: React.FocusEvent<HTMLInputElement>) => {
      setEstaEnFoco(true);
      const val = bigValActual.eq(0) ? '' : bigValActual.toString();
      setTextoLocal(val);
      setTimeout(() => e.target.select(), 0);
    };

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      const nuevoTexto = e.target.value;
      setTextoLocal(nuevoTexto);

      const bigVal = parseCOP(nuevoTexto);
      if (!permiteNegativos && bigVal.lt(0)) {
        onChange(0);
      } else {
        onChange(Number(bigVal.toFixed(2)));
      }
    };

    const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
      setEstaEnFoco(false);
      const bigVal = parseCOP(e.target.value);
      if (bigVal.eq(0)) {
        onChange(0);
      } else {
        onChange(Number(bigVal.toFixed(2)));
      }
    };

    return (
      <div className="w-full">
        {label && (
          <label
            htmlFor={inputId}
            className="block text-xs font-bold text-slate-600 uppercase tracking-wider mb-1.5"
          >
            {label}
            {required && <span className="text-rose-500 ml-1" aria-hidden="true">*</span>}
          </label>
        )}

        <div className="relative rounded-xl shadow-2xs">
          <span
            className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 font-semibold text-sm"
            aria-hidden="true"
          >
            $
          </span>

          <input
            ref={ref}
            id={inputId}
            type="text"
            inputMode="decimal"
            disabled={disabled}
            value={valorAMostrar}
            placeholder="0"
            onFocus={handleFocus}
            onChange={handleChange}
            onBlur={handleBlur}
            aria-invalid={!!error}
            aria-describedby={error ? errorId : undefined}
            className={cn(
              'block w-full rounded-xl border bg-white font-mono font-semibold text-sm text-slate-900 transition-all min-h-[44px]',
              'focus:outline-none focus:ring-2 focus:ring-blue-100 focus:border-blue-500 pl-8 pr-3.5 py-2.5',
              'disabled:bg-slate-50 disabled:text-slate-400 disabled:cursor-not-allowed',
              error
                ? 'border-rose-300 focus:border-rose-500 focus:ring-rose-100 bg-rose-50/20'
                : 'border-slate-200 hover:border-slate-300',
              className
            )}
            {...props}
          />
        </div>

        {error && (
          <p id={errorId} className="mt-1 text-xs text-rose-600 font-medium" role="alert">
            {error}
          </p>
        )}

        {!error && helperText && (
          <p className="mt-1 text-xs text-slate-400">
            {helperText}
          </p>
        )}
      </div>
    );
  }
);

MoneyInput.displayName = 'MoneyInput';
