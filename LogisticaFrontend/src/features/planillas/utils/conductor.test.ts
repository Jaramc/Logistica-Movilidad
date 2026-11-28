import { describe, it, expect } from 'vitest';
import { resolverConductor } from './conductor';

const conocidos = ['PEDRO PEREZ', 'ANDRES GOMEZ', 'ANDRES FELIPE RUIZ', 'JOSE ÁLVAREZ'];

describe('resolverConductor', () => {
  it('completa el nombre de pila leído en los saldos con el único conductor que coincide', () => {
    expect(resolverConductor('Pedro', conocidos)).toBe('PEDRO PEREZ');
  });

  it('ignora tildes, mayúsculas y espacios', () => {
    expect(resolverConductor('  josé ', conocidos)).toBe('JOSE ÁLVAREZ');
    expect(resolverConductor('pedro   perez', conocidos)).toBe('PEDRO PEREZ');
  });

  it('no adivina si hay más de un conductor con ese nombre', () => {
    expect(resolverConductor('Andres', conocidos)).toBe('Andres');
    expect(resolverConductor('Andres Felipe', conocidos)).toBe('ANDRES FELIPE RUIZ');
  });

  it('deja el nombre leído si no hay conductores conocidos que coincidan', () => {
    expect(resolverConductor('Carlos', conocidos)).toBe('Carlos');
    expect(resolverConductor('Pedro', [])).toBe('Pedro');
  });
});
