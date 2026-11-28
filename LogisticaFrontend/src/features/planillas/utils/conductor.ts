/** Normaliza como el backend: sin tildes para comparar, espacios simples y mayúsculas. */
const clave = (texto: string) =>
  texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim()
    .replace(/\s+/g, ' ')
    .toUpperCase();

/**
 * El OCR solo lee el nombre escrito junto a los saldos ("Pedro"). Si coincide con exactamente un
 * conductor conocido (el nombre completo o su primer nombre), devuelve ese conductor; si no hay
 * coincidencia o es ambigua (dos "PEDRO ..."), devuelve el nombre tal como se leyó.
 */
export function resolverConductor(leido: string, conocidos: readonly string[]): string {
  const buscado = clave(leido);
  if (!buscado) return leido;

  const exacto = conocidos.find((c) => clave(c) === buscado);
  if (exacto) return exacto;

  const palabras = buscado.split(' ');
  const candidatos = conocidos.filter((c) => {
    const partes = clave(c).split(' ');
    return palabras.every((p, i) => partes[i] === p);
  });
  return candidatos.length === 1 ? candidatos[0]! : leido;
}
