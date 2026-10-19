import { describe, it, expect, beforeEach } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';
import { use_offline_store } from '@/stores/offline_store';

describe('use_offline_store (Cola Offline e Idempotencia)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
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
});
