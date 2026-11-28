/** Fecha local en formato yyyy-MM-dd. */
export function hoyIso(): string {
  const d = new Date();
  const mes = String(d.getMonth() + 1).padStart(2, '0');
  const dia = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mes}-${dia}`;
}

/** Valida el rango en el cliente (el backend vuelve a validarlo): máximo 1 año y sin fechas futuras. */
export function validarRango(desde: string, hasta: string, hoy: string = hoyIso()): string | null {
  if (!desde || !hasta) return 'Seleccione la fecha inicial y la final.';
  if (hasta < desde) return 'La fecha final debe ser igual o posterior a la inicial.';
  if (desde > hoy) return 'La fecha inicial no puede ser futura.';
  const limite = new Date(`${desde}T00:00:00`);
  limite.setFullYear(limite.getFullYear() + 1);
  if (new Date(`${hasta}T00:00:00`) > limite) return 'El rango máximo por descarga es de 1 año.';
  return null;
}
