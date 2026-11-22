import { z } from 'zod';
import { toApiDecimalNumber } from '../utils/money';

/** Las 16 categorías del consolidado (mismo orden que ConceptoNormalizer.CategoriasCanonicas). */
export const CATEGORIAS_GASTO = [
  'PEAJES', 'PORCENTAJE', 'COMISION', 'PARQUEO', 'ACPM', 'ENGRASE', 'TENSIONADA', 'ASEO',
  'LLANTAS', 'UREA', 'CARGUE', 'DESCARGUE', 'CARROSADA', 'DESCARROSADA', 'PAPELERIA', 'OTROS',
] as const;

export type CategoriaGasto = (typeof CATEGORIAS_GASTO)[number];

export const esCategoriaGasto = (valor: unknown): valor is CategoriaGasto =>
  typeof valor === 'string' && (CATEGORIAS_GASTO as readonly string[]).includes(valor);

/** Mayúsculas sin tildes ni signos: "Nº Manifiesto" → "NO MANIFIESTO" (igual que TextoPlanilla.Normalizar). */
const normalizarTexto = (texto: string) =>
  texto
    .normalize('NFKD')
    .replace(/\p{M}/gu, '')
    .toUpperCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();

const PALABRAS_NO_GASTO = new Set([
  'EMPRESA', 'ORIGEN', 'DESTINO', 'FECHA', 'MANIFIESTO', 'PLACA', 'FLETE', 'ANTICIPO', 'TOTAL', 'SALDO', 'SALDOS',
]);

/** Igual que ListaNegraConceptos.Coincide en el backend: datos de cabecera o de saldos no son gastos. */
export function esDatoNoGasto(texto: string | null | undefined): boolean {
  const palabras = normalizarTexto(texto ?? '').split(' ').filter(Boolean);
  if (palabras.length === 0) return false;
  const primera = ['N', 'NO', 'NRO'].includes(palabras[0]!) && palabras.length > 1 ? palabras[1]! : palabras[0]!;
  return PALABRAS_NO_GASTO.has(primera);
}

/** Lo que se muestra y se guarda como concepto: la etiqueta impresa, si no la descripción. */
export const conceptoDeGasto = (g: { etiqueta: string; descripcion: string }) =>
  g.etiqueta.trim() || g.descripcion.trim() || 'Gasto sin descripción';

/**
 * Esquema para un renglón de gasto en la interfaz: etiqueta impresa (vacía en filas libres),
 * descripción escrita a mano, valor y categoría (una de las 16).
 */
export const GastoItemSchema = z
  .object({
    id: z.string().min(1, 'ID temporal requerido'),
    etiqueta: z.string(),
    descripcion: z.string().max(200, 'La descripción no puede exceder 200 caracteres'),
    categoria: z.enum(CATEGORIAS_GASTO, { message: 'Elige una categoría' }),
    valor: z.number().min(0, 'El valor no puede ser negativo'),
  })
  .superRefine((g, ctx) => {
    if (!g.etiqueta.trim() && !g.descripcion.trim()) {
      ctx.addIssue({ code: 'custom', path: ['descripcion'], message: 'Escribe una descripción del gasto' });
    }
    if (esDatoNoGasto(g.etiqueta) || esDatoNoGasto(g.descripcion)) {
      ctx.addIssue({ code: 'custom', path: ['descripcion'], message: 'Esto es un dato de cabecera o de saldos, no un gasto' });
    }
  });

export type GastoItem = z.infer<typeof GastoItemSchema>;

/**
 * Saldo escrito en la planilla. valor con signo: positivo = la empresa le debe al conductor;
 * negativo = el conductor le debe a la empresa.
 */
export const SaldoDeclaradoSchema = z
  .object({
    valor: z.number(),
    aFavorDe: z.enum(['Conductor', 'Empresa']).nullish().transform(v => v ?? null),
  })
  .nullish()
  .transform(v => v ?? null);

export type SaldoDeclarado = NonNullable<z.infer<typeof SaldoDeclaradoSchema>>;

/**
 * Esquema de validación en runtime para la respuesta del OCR de Azure AI Document Intelligence.
 * Es tolerante a nombres de campos nulos o variantes del modelo sin romper la UI.
 */
export const PlanillaOcrResponseSchema = z.object({
  mensaje: z.string().optional(),
  advertencias: z.array(z.string()).optional(),
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
        /** Etiqueta impresa ("PEAJES"); vacía en filas libres. */
        etiqueta: z.string().nullish().transform(val => (val ?? '').trim()),
        /** Lo escrito a mano en la fila. */
        descripcion: z.string().nullish().transform(val => (val ?? '').trim()),
        categoriaPropuesta: z.string().nullish().transform(val => (esCategoriaGasto(val) ? val : 'OTROS')),
        confianza: z.number().nullish().transform(val => val ?? null),
      })
    ).nullish().transform(val => val ?? []),
    conductor: z.string().nullish().transform(val => (val ?? '').trim()),
    empresaTransportadora: z.string().nullish().transform(val => (val ?? '').trim()),
    manifiesto: z.string().nullish().transform(val => (val ?? '').trim()),
    /** yyyy-MM-dd o null si el OCR no pudo leer la fecha. */
    fechaInicial: z.string().nullish().transform(val => val ?? null),
    fechaFinal: z.string().nullish().transform(val => val ?? null),
    /** Advertencias del OCR (no bloquean): diferencias de sumas, campos ilegibles, etc. */
    advertencias: z.array(z.string()).nullish().transform(val => val ?? []),
    /** Promedio de confianza (0–1) de los campos leídos por el modelo. */
    confianzaPromedio: z.number().min(0).max(1).nullish().transform(val => val ?? null),
    /** Campos del modelo que no vinieron (p. ej. "Flete"); la UI los resalta. */
    camposNoEncontrados: z.array(z.string()).nullish().transform(val => val ?? []),
    /** Copia de auditoría de los campos leídos; se reenvía tal cual al guardar. */
    jsonOcrCrudo: z.string().nullish().transform(val => val ?? null),
    /** Lo escrito en papel (no lo calculado): se muestra al revisor y se guarda para auditoría. */
    totalGastosDeclarado: z.number().nullish().transform(val => val ?? null),
    saldoViajeDeclarado: SaldoDeclaradoSchema,
    saldoAnteriorDeclarado: SaldoDeclaradoSchema,
    saldoNuevoDeclarado: SaldoDeclaradoSchema,
  }).nullish(),
});

export type PlanillaOcrResponse = z.infer<typeof PlanillaOcrResponseSchema>;

/**
 * Esquema del formulario completo de edición y liquidación (React Hook Form).
 * Tipos directos para perfecta compatibilidad con useForm sin divergencia input/output.
 */
/** "STW 514" / "stw-514" → "STW514" (igual que Placa.Normalizar en el backend). */
export const normalizarPlaca = (placa: string) => placa.replace(/[\s-]/g, '').toUpperCase();

/** yyyy-MM-dd que además es una fecha real (rechaza 2026-02-30). */
const esFechaIsoValida = (valor: string) => {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(valor)) return false;
  const [a, m, d] = valor.split('-').map(Number);
  const fecha = new Date(Date.UTC(a!, m! - 1, d!));
  return fecha.getUTCFullYear() === a && fecha.getUTCMonth() === m! - 1 && fecha.getUTCDate() === d;
};

export const PlanillaFormSchema = z.object({
  // Datos Generales del Viaje
  placa: z.string()
    .trim()
    .refine(v => /^[A-Z]{3}\d{3}$/.test(normalizarPlaca(v)), 'La placa debe tener 3 letras y 3 números (p. ej. STW514)'),
  conductor: z.string().trim().min(2, 'El nombre del conductor es requerido'),
  origen: z.string().trim().min(2, 'La ciudad de origen es requerida'),
  destino: z.string().trim().min(2, 'La ciudad de destino es requerida'),
  fechaInicial: z.string().refine(esFechaIsoValida, 'Fecha inicial válida requerida'),
  fechaFinal: z.string().refine(esFechaIsoValida, 'Fecha final válida requerida'),
  manifiesto: z.string(),
  empresaTransportadora: z.string()
    .trim()
    .refine(v => v === '' || /\p{L}.*\p{L}/u.test(v), 'La empresa debe tener letras'),

  // Rubros Económicos Principales
  flete: z.number().min(0, 'El flete debe ser mayor o igual a 0'),
  anticipo: z.number().min(0, 'El anticipo debe ser mayor o igual a 0'),
  saldoAnterior: z.number(),

  // Gastos Dinámicos
  detalleGastos: z.array(GastoItemSchema),
}).superRefine((v, ctx) => {
  if (esFechaIsoValida(v.fechaInicial) && esFechaIsoValida(v.fechaFinal) && v.fechaFinal < v.fechaInicial) {
    ctx.addIssue({ code: 'custom', path: ['fechaFinal'], message: 'La fecha final no puede ser anterior a la inicial' });
  }
});

export type PlanillaFormData = z.infer<typeof PlanillaFormSchema>;

/**
 * DTO de Comando estricto para Guardar en Base de Datos (Anti-Overposting).
 * Se excluyen 'id', 'creadoEn', 'estadoRevision' y TODOS los totales:
 * el servidor los recalcula con decimal y los devuelve en la respuesta.
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
  gastos: z.array(
    z.object({
      concepto: z.string().min(1),
      valor: z.number(),
      etiqueta: z.string().nullable().optional(),
      descripcion: z.string().nullable().optional(),
      /** Categoría elegida en la UI (una de las 16); el servidor la valida. */
      categoria: z.enum(CATEGORIAS_GASTO).nullable().optional(),
    })
  ),
  /** El jsonOcrCrudo que devolvió /extraer. El servidor lo valida (≤ 256 KB, JSON) o guarda null. */
  jsonOcrCrudo: z.string().nullable().optional(),
  /** Valores escritos en papel (con signo). Solo auditoría: el servidor nunca los usa para calcular. */
  totalGastosDeclarado: z.number().nullable().optional(),
  saldoViajeDeclarado: z.number().nullable().optional(),
  saldoAnteriorDeclarado: z.number().nullable().optional(),
  saldoNuevoDeclarado: z.number().nullable().optional(),
});

export type GuardarPlanillaDto = z.infer<typeof GuardarPlanillaDtoSchema>;

/**
 * Esquema de resumen de planilla para listados e historial.
 */
export const PlanillaResumenSchema = z.object({
  id: z.number(),
  numeroPlanilla: z.string().nullable().optional(),
  placa: z.string(),
  conductor: z.string().nullable().optional(),
  origen: z.string().nullable().optional().transform(v => v ?? 'N/A'),
  destino: z.string().nullable().optional().transform(v => v ?? 'N/A'),
  fechaInicial: z.string().nullable().optional(),
  fechaFinal: z.string().nullable().optional(),
  fechaViaje: z.string().nullable().optional(),
  totalGastos: z.number(),
  saldoViaje: z.number(),
  saldoTotal: z.number().optional(),
  estado: z.string().optional(),
  estadoRevision: z.string().optional(),
  creadoEn: z.string().optional(),
  createdAt: z.string().optional(),
}).transform(v => ({
  ...v,
  estado: v.estado ?? v.estadoRevision ?? 'Aprobado',
}));

export type PlanillaResumen = z.infer<typeof PlanillaResumenSchema>;

/**
 * Esquema paginado para el historial de planillas (PagedResultDto<T> del backend).
 */
export const PagedPlanillasSchema = z.object({
  items: z.array(PlanillaResumenSchema),
  totalCount: z.number().optional(),
  totalItems: z.number().optional(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
}).transform(val => ({
  items: val.items,
  totalItems: val.totalItems ?? val.totalCount ?? val.items.length,
  totalCount: val.totalCount ?? val.totalItems ?? val.items.length,
  page: val.page,
  pageSize: val.pageSize,
  totalPages: val.totalPages,
}));

export type PagedPlanillas = z.infer<typeof PagedPlanillasSchema>;

