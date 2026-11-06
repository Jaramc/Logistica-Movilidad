export interface GastoDetalle {
  concepto: string;
  valor: number;
}

export interface LiquidacionViaje {
  id?: number;
  fechaInicial: string;
  fechaFinal: string;
  conductor: string;
  placa: string;
  manifiesto: string;
  empresaTransportadora: string;
  origenViaje: string;
  destino: string;

  // Campos específicos de Planilla de Gastos (Azure OCR)
  flete: number;
  anticipo: number;
  detalleGastos: GastoDetalle[];

  // Totales y Saldos
  totalGastos: number;
  traslado: number;
  basico: number;
  saldoViaje: number;
  saldoAnterior: number;
  saldoTotal: number;

  // Auditoría
  estadoRevision: 'pendiente' | 'aprobado';
  jsonOcrCrudo?: string | null;
  creadoEn?: string;
  actualizadoEn?: string;
}

export const VALORES_INICIALES_LIQUIDACION: LiquidacionViaje = {
  fechaInicial: new Date().toISOString().split('T')[0] ?? '',
  fechaFinal: new Date().toISOString().split('T')[0] ?? '',
  conductor: '',
  placa: '',
  manifiesto: '',
  empresaTransportadora: '',
  origenViaje: '',
  destino: '',

  flete: 0,
  anticipo: 0,
  detalleGastos: [],

  totalGastos: 0,
  traslado: 0,
  basico: 0,
  saldoViaje: 0,
  saldoAnterior: 0,
  saldoTotal: 0,

  estadoRevision: 'pendiente',
  jsonOcrCrudo: null
};
