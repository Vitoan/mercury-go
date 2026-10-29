import { describe, it, expect, beforeEach, vi } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';

vi.mock('@/services/token_service', () => ({
  obtener_token: vi.fn(async () => 'token-de-prueba')
}));

vi.mock('@/services/pedidos_service', () => ({
  pedidos_service: {
    cambiar_estado: vi.fn()
  }
}));

vi.mock('@/services/notificaciones_service', () => ({
  notificaciones_service: {
    disparar_notificacion_local: vi.fn(async () => {})
  }
}));

vi.mock('@/stores/pedidos_store', () => ({
  usePedidosStore: () => ({ cargar: vi.fn(async () => {}) })
}));

import { use_offline_store } from '@/stores/offline_store';
import { pedidos_service } from '@/services/pedidos_service';

const errorDeConflicto = () => {
  const error = new Error('El pedido fue cancelado y no puede ser marcado como entregado.');
  error.estado_http = 400;
  error.codigo = 'pedido_cancelado';
  return error;
};

describe('use_offline_store (Cola Offline e Idempotencia)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('encola una acción con UUID v4 e Idempotency-Key válido', () => {
    const store = use_offline_store();
    const accion = store.encolar_accion('ENTREGAR', 101, { estado: 'Entregado' }, -31.4, -64.1);

    expect(accion).toBeDefined();
    expect(accion.id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    expect(accion.tipo).toBe('ENTREGAR');
    expect(accion.pedido_id).toBe(101);
    expect(accion.estado).toBe('pendiente');
    expect(store.cola.length).toBe(1);
    expect(store.pendientes_count).toBe(1);
  });

  it('permite limpiar acciones ya enviadas conservando las pendientes', () => {
    const store = use_offline_store();
    const a1 = store.encolar_accion('EN_PUERTA', 201);
    const a2 = store.encolar_accion('ENTREGAR', 201);

    a1.estado = 'enviada';
    store.guardar_cola([a1, a2]);

    expect(store.cola.length).toBe(2);
    expect(store.pendientes_count).toBe(1);

    store.limpiar_enviadas();
    expect(store.cola.length).toBe(1);
    expect(store.cola[0].id).toBe(a2.id);
  });

  it('marca como conflicto un rechazo 400, bloquea las posteriores y reenvía como offline', async () => {
    const store = use_offline_store();
    const a1 = store.encolar_accion('EN_PUERTA', 500, { ocurrido_en: '2026-10-29T12:00:00.000Z' });
    const a2 = store.encolar_accion('ENTREGAR', 500, {});

    pedidos_service.cambiar_estado.mockRejectedValueOnce(errorDeConflicto());

    await store.sincronizar();

    const conflicto = store.cola.find((a) => a.id === a1.id);
    const bloqueada = store.cola.find((a) => a.id === a2.id);

    expect(conflicto.estado).toBe('conflicto');
    expect(conflicto.codigo).toBe('pedido_cancelado');
    expect(bloqueada.estado).toBe('bloqueada');
    expect(store.conflictos_count).toBe(1);

    // No se sigue enviando la siguiente acción del mismo pedido
    expect(pedidos_service.cambiar_estado).toHaveBeenCalledTimes(1);
    // El replay se identifica como offline para que la excepción de la Decisión 10 aplique
    expect(pedidos_service.cambiar_estado.mock.calls[0][6]).toBe(true);
  });

  it('descartar una acción en conflicto libera las bloqueadas de la misma orden', async () => {
    const store = use_offline_store();
    const a1 = store.encolar_accion('EN_PUERTA', 600);
    const a2 = store.encolar_accion('ENTREGAR', 600);

    pedidos_service.cambiar_estado.mockRejectedValueOnce(errorDeConflicto());
    await store.sincronizar();

    expect(store.cola.find((a) => a.id === a2.id).estado).toBe('bloqueada');

    // Sin red para que descartar no dispare una sincronización en segundo plano
    store.en_linea = false;
    store.descartar_accion(a1.id);

    expect(store.cola.length).toBe(1);
    expect(store.cola[0].id).toBe(a2.id);
    expect(store.cola[0].estado).toBe('pendiente');
  });

  it('no cuenta conflictos ni bloqueadas como pendientes', () => {
    const store = use_offline_store();
    store.guardar_cola([
      { id: 'a', pedido_id: 1, estado: 'conflicto' },
      { id: 'b', pedido_id: 1, estado: 'bloqueada' },
      { id: 'c', pedido_id: 2, estado: 'pendiente' }
    ]);

    expect(store.pendientes_count).toBe(1);
    expect(store.conflictos_count).toBe(1);
  });
});
