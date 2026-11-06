import { z } from 'zod';
import { toApiDecimalNumber } from '../utils/money';

/**
 * Esquema para un renglón individual de gastos en la interfaz dinámica.
 */
export const GastoItemSchema = z.object({
  id: z.string().min(1, 'ID temporal requerido'),
  concepto: z.string().trim().min(1, 'El concepto es obligatorio'),
  valor: z.number().min(0, 'El valor no puede ser negativo'),
});

export type GastoItem = z.infer<typeof GastoItemSchema>;

/**
 * Esquema de validación en runtime para la respuesta del OCR de Azure AI Document Intelligence.
 * Es tolerante a nombres de campos nulos o variantes del modelo sin romper la UI.
 */
export const PlanillaOcrResponseSchema = z.object({
  mensaje: z.string().optional(),
  data: z.object({
    placa: z.string().nullish().transform(val => (val ?? '').trim().toUpperCase()),
    origen: z.string().nullish().transform(val => (val ?? '').trim()),
    destino: z.string().nullish().transform(val => (val ?? '').trim()),
    flete: z.union([z.number(), z.string()]).nullish().transform(val => toApiDecimalNumber(val ?? 0)),
    anticipo: z.union([z.number(), z.string()]).nullish().transform(val => toApiDecimalNumber(val ?? 0)),
    totalGastos: z.union([z.number(), z.string()]).nullish().transform(val => toApiDecimalNumber(val ?? 0)),
    saldoViaje: z.union([z.number(), z.string()]).nullish().transform(val => toApiDecimalNumber(val ?? 0)),
    detalleGastos: z.array(
      z.object({
        concepto: z.union([z.string(), z.number()]).nullish().transform(val => String(val ?? 'Gasto').trim()),
        valor: z.union([z.number(), z.string()]).nullish().transform(val => toApiDecimalNumber(val ?? 0)),
      })
    ).nullish().transform(val => val ?? []),
  }).nullish(),
});

export type PlanillaOcrResponse = z.infer<typeof PlanillaOcrResponseSchema>;

/**
 * Esquema del formulario completo de edición y liquidación (React Hook Form).
 * Tipos directos para perfecta compatibilidad con useForm sin divergencia input/output.
 */
export const PlanillaFormSchema = z.object({
  // Datos Generales del Viaje
  placa: z.string()
    .trim()
    .min(3, 'La placa debe tener al menos 3 caracteres')
    .max(10, 'La placa no puede exceder 10 caracteres'),
  conductor: z.string().trim().min(2, 'El nombre del conductor es requerido'),
  origen: z.string().trim().min(2, 'La ciudad de origen es requerida'),
  destino: z.string().trim().min(2, 'La ciudad de destino es requerida'),
  fechaInicial: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Fecha inicial en formato YYYY-MM-DD requerida'),
  fechaFinal: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Fecha final en formato YYYY-MM-DD requerida'),
  manifiesto: z.string(),
  empresaTransportadora: z.string(),

  // Rubros Económicos Principales
  flete: z.number().min(0, 'El flete debe ser mayor o igual a 0'),
  anticipo: z.number().min(0, 'El anticipo debe ser mayor o igual a 0'),
  saldoAnterior: z.number(),

  // Gastos Dinámicos
  detalleGastos: z.array(GastoItemSchema),
});

export type PlanillaFormData = z.infer<typeof PlanillaFormSchema>;

/**
 * DTO de Comando estricto para Guardar en Base de Datos (Anti-Overposting).
 * Se excluyen deliberadamente 'id', 'creadoEn', 'actualizadoEn' y 'estadoRevision'.
 */
export const GuardarPlanillaDtoSchema = z.object({
  placa: z.string().min(1),
  conductor: z.string().min(1),
  origen: z.string().min(1),
  destino: z.string().min(1),
  fechaInicial: z.string(),
  fechaFinal: z.string(),
  manifiesto: z.string().optional(),
  empresaTransportadora: z.string().optional(),
  flete: z.number(),
  anticipo: z.number(),
  totalGastos: z.number(),
  saldoViaje: z.number(),
  saldoAnterior: z.number(),
  saldoTotal: z.number(),
  gastos: z.array(
    z.object({
      concepto: z.string().min(1),
      valor: z.number(),
    })
  ),
});

export type GuardarPlanillaDto = z.infer<typeof GuardarPlanillaDtoSchema>;
