import { describe, it, expect } from 'vitest';
import { hoyLocalISO } from './fechas';

describe('hoyLocalISO', () => {
  it('usa la fecha local, no la UTC: 20 de noviembre a las 8 p. m. sigue siendo el 20', () => {
    // Constructor local: 2026-11-20 20:00 hora del navegador. En Colombia, en UTC ya es el 21.
    expect(hoyLocalISO(new Date(2026, 10, 20, 20, 0, 0))).toBe('2026-11-20');
  });

  it('rellena mes y día con cero', () => {
    expect(hoyLocalISO(new Date(2026, 0, 5, 23, 59, 59))).toBe('2026-01-05');
  });
});
