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

  // Gastos Operativos (16 campos)
  peajes: number;
  porcentaje: number;
  comision: number;
  parqueo: number;
  acpm: number;
  engrase: number;
  tensionada: number;
  aseo: number;
  llantas: number;
  urea: number;
  cargue: number;
  descargue: number;
  carrosada: number;
  descarrosada: number;
  papeleria: number;
  otros: number;

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
  fechaInicial: new Date().toISOString().split('T')[0],
  fechaFinal: new Date().toISOString().split('T')[0],
  conductor: '',
  placa: '',
  manifiesto: '',
  empresaTransportadora: '',
  origenViaje: '',
  destino: '',

  peajes: 0,
  porcentaje: 0,
  comision: 0,
  parqueo: 0,
  acpm: 0,
  engrase: 0,
  tensionada: 0,
  aseo: 0,
  llantas: 0,
  urea: 0,
  cargue: 0,
  descargue: 0,
  carrosada: 0,
  descarrosada: 0,
  papeleria: 0,
  otros: 0,

  totalGastos: 0,
  traslado: 0,
  basico: 0,
  saldoViaje: 0,
  saldoAnterior: 0,
  saldoTotal: 0,

  estadoRevision: 'pendiente',
  jsonOcrCrudo: null
};
