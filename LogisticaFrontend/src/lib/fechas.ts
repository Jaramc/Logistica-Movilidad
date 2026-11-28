/**
 * Fecha de hoy en la zona horaria del navegador, formato yyyy-MM-dd (para <input type="date">).
 * No usar toISOString(): devuelve la fecha UTC y en Colombia (UTC-5), después de las 7 p. m.,
 * daría la fecha de mañana.
 */
export function hoyLocalISO(ahora: Date = new Date()): string {
  const anio = ahora.getFullYear();
  const mes = String(ahora.getMonth() + 1).padStart(2, '0');
  const dia = String(ahora.getDate()).padStart(2, '0');
  return `${anio}-${mes}-${dia}`;
}
