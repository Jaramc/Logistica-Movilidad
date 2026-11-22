import { useRef } from 'react';
import { type Control, useFieldArray, Controller } from 'react-hook-form';
import { CATEGORIAS_GASTO, type PlanillaFormData } from '../schemas/planillaSchema';
import { MoneyInput } from '../../../components/ui/MoneyInput';
import { Button } from '../../../components/ui/Button';
import { Plus, Trash2, Receipt, Calculator } from 'lucide-react';

export interface GastosTableProps {
  control: Control<PlanillaFormData>;
}

/** Nombre legible de la categoría en el selector. */
const NOMBRE_CATEGORIA: Record<(typeof CATEGORIAS_GASTO)[number], string> = {
  PEAJES: 'Peajes', PORCENTAJE: 'Porcentaje', COMISION: 'Comisión', PARQUEO: 'Parqueo', ACPM: 'ACPM',
  ENGRASE: 'Engrase', TENSIONADA: 'Tensionada', ASEO: 'Aseo', LLANTAS: 'Llantas', UREA: 'Urea',
  CARGUE: 'Cargue', DESCARGUE: 'Descargue', CARROSADA: 'Carrosada', DESCARROSADA: 'Descarrosada',
  PAPELERIA: 'Papelería', OTROS: 'Otros',
};

/**
 * Grilla de rubros: solo gastos. La cabecera (empresa, fechas, placa, flete...) y los saldos
 * van en sus propios bloques; nunca aquí.
 */
export const GastosTable: React.FC<GastosTableProps> = ({ control }) => {
  const { fields, append, remove } = useFieldArray({
    control,
    name: 'detalleGastos',
    keyName: '_rhfId', // Preserva la clave interna de RHF separada del campo id
  });

  const lastAddedInputRef = useRef<HTMLInputElement>(null);

  const agregarNuevoGasto = () => {
    const nuevoId = typeof crypto !== 'undefined' && crypto.randomUUID
      ? crypto.randomUUID()
      : `gasto_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;

    append({ id: nuevoId, etiqueta: '', descripcion: '', categoria: 'OTROS', valor: 0 });

    // Foco automático en el nuevo rubro para agilidad del digitador
    setTimeout(() => {
      lastAddedInputRef.current?.focus();
    }, 50);
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-3 border-b border-slate-100 gap-3">
        <div>
          <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider flex items-center gap-2">
            <Receipt className="w-4 h-4 text-blue-600" />
            <span>Rubros de Gastos Extraídos y Editables</span>
          </h4>
          <p className="text-xs text-slate-500 mt-0.5">
            {fields.length > 0
              ? `${fields.length} rubros detectados. Revisa la descripción, el valor y la categoría de cada uno.`
              : 'No hay gastos registrados en esta planilla.'}
          </p>
        </div>

        <Button
          type="button"
          size="sm"
          variant="outline"
          onClick={agregarNuevoGasto}
          leftIcon={<Plus className="w-3.5 h-3.5" />}
          className="self-start sm:self-auto text-blue-600 hover:text-blue-700 hover:bg-blue-50 border-blue-200"
        >
          Agregar Rubro
        </Button>
      </div>

      {fields.length === 0 ? (
        <div className="py-12 text-center border-2 border-dashed border-slate-200 rounded-2xl bg-slate-50/50">
          <Calculator className="w-10 h-10 text-slate-300 mx-auto mb-2" aria-hidden="true" />
          <p className="text-sm font-semibold text-slate-700">Sin rubros de gastos</p>
          <p className="text-xs text-slate-400 mt-1 max-w-sm mx-auto mb-4">
            Sube un comprobante para que Azure AI extraiga los gastos o agrégalos manualmente.
          </p>
          <Button
            type="button"
            size="sm"
            onClick={agregarNuevoGasto}
            leftIcon={<Plus className="w-3.5 h-3.5" />}
          >
            Crear Primer Rubro
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
          {fields.map((field, index) => {
            const isLast = index === fields.length - 1;
            const nombre = field.etiqueta || field.descripcion || `rubro ${index + 1}`;
            return (
              <div
                key={field.id} // CLAVE ESTABLE UUID (Soluciona bug de renderizado y foco)
                data-testid="tarjeta-gasto"
                className="bg-slate-50/90 p-3.5 rounded-xl border border-slate-200/80 hover:border-blue-300 hover:bg-white transition-all shadow-2xs group focus-within:border-blue-500 focus-within:bg-white focus-within:ring-2 focus-within:ring-blue-100 space-y-2"
              >
                <div className="flex items-center justify-between">
                  <span
                    className={`text-[11px] font-bold uppercase tracking-wide px-2 py-0.5 rounded-md ${
                      field.etiqueta ? 'bg-blue-50 text-blue-800 border border-blue-100' : 'bg-slate-100 text-slate-500 border border-slate-200'
                    }`}
                    data-testid="etiqueta-gasto"
                  >
                    {field.etiqueta || 'Libre'}
                  </span>

                  <button
                    type="button"
                    onClick={() => remove(index)}
                    className="text-slate-300 hover:text-rose-600 p-1.5 rounded-lg transition-colors cursor-pointer opacity-70 group-hover:opacity-100 hover:bg-rose-50"
                    title={`Eliminar rubro ${nombre}`}
                    aria-label={`Eliminar rubro ${nombre}`}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>

                <Controller
                  control={control}
                  name={`detalleGastos.${index}.descripcion`}
                  render={({ field: descripcionField, fieldState }) => (
                    <div>
                      <input
                        ref={isLast ? lastAddedInputRef : undefined}
                        type="text"
                        placeholder={field.etiqueta ? 'Nota escrita a mano (opcional)' : 'Descripción del gasto'}
                        value={descripcionField.value}
                        onChange={descripcionField.onChange}
                        onBlur={descripcionField.onBlur}
                        className="text-xs text-slate-800 bg-transparent border-b border-slate-200 hover:border-slate-300 focus:border-blue-500 focus:outline-none w-full py-0.5 transition-colors"
                        aria-label={`Descripción del gasto ${index + 1}`}
                        aria-invalid={!!fieldState.error}
                      />
                      {fieldState.error && (
                        <p className="text-[11px] text-rose-600 mt-0.5" role="alert">{fieldState.error.message}</p>
                      )}
                    </div>
                  )}
                />

                <Controller
                  control={control}
                  name={`detalleGastos.${index}.valor`}
                  render={({ field: valorField, fieldState }) => (
                    <MoneyInput
                      value={valorField.value}
                      onChange={valorField.onChange}
                      error={fieldState.error?.message}
                      aria-label={`Valor del gasto ${nombre}`}
                    />
                  )}
                />

                <Controller
                  control={control}
                  name={`detalleGastos.${index}.categoria`}
                  render={({ field: categoriaField }) => (
                    <select
                      value={categoriaField.value}
                      onChange={categoriaField.onChange}
                      onBlur={categoriaField.onBlur}
                      className="w-full text-xs border border-slate-200 rounded-lg px-2 py-1.5 bg-white focus:border-blue-500 focus:outline-none"
                      aria-label={`Categoría del gasto ${nombre}`}
                    >
                      {CATEGORIAS_GASTO.map((c) => (
                        <option key={c} value={c}>{NOMBRE_CATEGORIA[c]}</option>
                      ))}
                    </select>
                  )}
                />
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
