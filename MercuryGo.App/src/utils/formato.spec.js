import { describe, it, expect } from 'vitest';
import { formatearMoneda, formatearFechaHora } from './formato';

describe('formato util', () => {
  describe('formatearMoneda', () => {
    it('formatea montos enteros correctamente', () => {
      const res = formatearMoneda(2400);
      expect(res).toContain('2.400');
      expect(res.startsWith('$')).toBe(true);
    });

    it('formatea con 2 decimales cuando se solicita', () => {
      const res = formatearMoneda(1250.5, true);
      expect(res).toContain('1.250,50');
      expect(res.startsWith('$')).toBe(true);
    });

    it('maneja valores nulos o invalidos de forma segura', () => {
      expect(formatearMoneda(null)).toBe('$0');
      expect(formatearMoneda(undefined)).toBe('$0');
      expect(formatearMoneda('invalido')).toBe('$0');
    });
  });

  describe('formatearFechaHora', () => {
    it('retorna vacio ante fechas nulas o invalidas', () => {
      expect(formatearFechaHora(null)).toBe('');
      expect(formatearFechaHora('fecha-invalida')).toBe('');
    });

    it('formatea fechas validas', () => {
      const resultado = formatearFechaHora('2026-08-25T14:30:00Z');
      expect(resultado).not.toBe('');
      expect(resultado).toContain('2026');
    });
  });
});
