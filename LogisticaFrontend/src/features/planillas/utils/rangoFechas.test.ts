import { describe, it, expect } from 'vitest';
import { validarRango } from './rangoFechas';

describe('validarRango (descarga del consolidado)', () => {
  const hoy = '2026-11-13';

  it('acepta un rango válido y uno de exactamente 1 año', () => {
    expect(validarRango('2026-01-01', '2026-06-30', hoy)).toBeNull();
    expect(validarRango('2025-11-13', '2026-11-13', hoy)).toBeNull();
  });

  it('rechaza más de 1 año, orden invertido, fechas futuras y vacíos', () => {
    expect(validarRango('2025-11-12', '2026-11-13', hoy)).toMatch(/1 año/);
    expect(validarRango('2026-06-30', '2026-01-01', hoy)).toMatch(/posterior/);
    expect(validarRango('2026-11-14', '2026-11-20', hoy)).toMatch(/futura/);
    expect(validarRango('', '2026-01-01', hoy)).not.toBeNull();
  });
});
