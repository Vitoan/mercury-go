import { describe, it, expect } from 'vitest';
import {
  estado_pedido,
  color_estado_pedido,
  texto_estado_pedido,
  siguiente_estado_pedido,
  rol_usuario,
  estado_comprobante,
  clase_tinte
} from './estados';

describe('utils/estados.js', () => {
  it('retorna texto, color e icono para estados válidos de pedidos', () => {
    const entregado = estado_pedido('Entregado');
    expect(entregado.texto).toBe('Entregado');
    expect(entregado.color).toBe('success');
    expect(entregado.icono).toBeDefined();

    const enPrep = estado_pedido('EnPreparacion');
    expect(enPrep.texto).toBe('En Depósito');
    expect(enPrep.color).toBe('warning');

    const despachado = estado_pedido('Despachado');
    expect(despachado.texto).toBe('En Ruta');
    expect(despachado.color).toBe('primary');
  });

  it('normaliza mayúsculas, minúsculas y snake_case sin romperse', () => {
    expect(color_estado_pedido('en_preparacion')).toBe('warning');
    expect(texto_estado_pedido('DESPACHADO')).toBe('En Ruta');
    expect(color_estado_pedido('no_entregado')).toBe('danger');
    expect(texto_estado_pedido('cancelado')).toBe('Cancelado');
  });

  it('proporciona fallback seguro para estados nulos o desconocidos', () => {
    const desc = estado_pedido('Inexistente');
    expect(desc.texto).toBe('Inexistente');
    expect(desc.color).toBe('medium');
    expect(desc.icono).toBeDefined();

    const nulo = estado_pedido(null);
    expect(nulo.texto).toBe('Desconocido');
    expect(nulo.color).toBe('medium');
  });

  it('calcula el siguiente estado habitual del pedido', () => {
    expect(siguiente_estado_pedido('Pendiente')).toBe('Confirmado');
    expect(siguiente_estado_pedido('confirmado')).toBe('EnPreparacion');
    expect(siguiente_estado_pedido('EnPreparacion')).toBe('Despachado');
    expect(siguiente_estado_pedido('Despachado')).toBe('EnPuerta');
    expect(siguiente_estado_pedido('Entregado')).toBe('');
  });

  it('identifica roles de usuario y estados de comprobantes correctamente', () => {
    const admin = rol_usuario('ADMIN');
    expect(admin.texto).toBe('Administrador');
    expect(admin.color).toBe('primary');

    const chofer = rol_usuario('chofer');
    expect(chofer.texto).toBe('Chofer Reparto');
    expect(chofer.color).toBe('success');

    const compAprobado = estado_comprobante('aprobado');
    expect(compAprobado.color).toBe('success');
  });

  it('genera clases de tinte semántico', () => {
    expect(clase_tinte('success')).toBe('mercury-tinte-success');
    expect(clase_tinte(null)).toBe('mercury-tinte-medium');
  });
});
