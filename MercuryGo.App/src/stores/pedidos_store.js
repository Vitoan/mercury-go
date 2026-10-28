import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { pedidos_service } from '@/services/pedidos_service';
import { use_offline_store } from '@/stores/offline_store';

export function con_cambios_sin_enviar(pedido) {
  if (!pedido) return pedido;
  const offline = use_offline_store();
  const cambios = (offline.cola || []).filter((c) => c.pedido_id == pedido.id);
  if (cambios.length === 0) return pedido;
  const ultimo = cambios[cambios.length - 1];
  return {
    ...pedido,
    estado: ultimo.estado,
    sin_enviar: true
  };
}

export const usePedidosStore = defineStore('pedidos', () => {
  const cargando = ref(false);
  const error = ref(null);
  const pedidos = ref([]);
  const conteos_por_estado = ref([]);
  const pagina = ref({
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  });
  const filtros = ref({
    busqueda: '',
    estados: ''
  });

  const hay_pedidos = computed(() => pedidos.value.length > 0);
  const total = computed(() => pagina.value.total);
  const mostrados = computed(() => pedidos.value.length);
  const hay_mas = computed(() => pagina.value.hay_mas);

  async function cargar_resumen() {
    try {
      const data = await pedidos_service.obtener_resumen();
      conteos_por_estado.value = data.conteos_por_estado || [];
    } catch {
      // Silencioso para no romper la vista si falla el conteo auxiliar
    }
  }

  async function cargar(resetear = false, event = null) {
    cargando.value = true;
    error.value = null;

    if (resetear) {
      pagina.value.pagina = 1;
    }

    try {
      await cargar_resumen();

      const res = await pedidos_service.obtener_pedidos({
        busqueda: filtros.value.busqueda,
        estados: filtros.value.estados,
        pagina: pagina.value.pagina,
        tamano: pagina.value.tamano
      });

            const pedidosDeApi = (res.pedidos || []).map(con_cambios_sin_enviar);
      const offline = use_offline_store();
      const pedidosEncolados = (offline.cola || [])
        .filter((c) => c.tipo === 'CREAR_PEDIDO' && c.estado !== 'enviada')
        .map((c) => {
          const detalles = c.payload?.detalles || [];
          const totalCalculado = detalles.reduce((acc, d) => acc + ((d.precio_unitario || 0) * (d.cantidad || 1)), 0);
          return {
            id: 'offline-' + c.id,
            numero: 'PED-OFFLINE-' + c.id.slice(0, 6).toUpperCase(),
            cliente_razon_social: c.payload?.cliente_nombre || 'Orden Fuera de Línea',
            fecha_pedido: c.ocurrido_en,
            estado: 'Pendiente',
            total: totalCalculado || (c.payload?.total || 0),
            observaciones: c.payload?.observaciones,
            sin_enviar: true,
            es_creado_offline: true
          };
        });
      const nuevos = [...pedidosEncolados, ...pedidosDeApi];
      pagina.value = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

      if (resetear || pagina.value.pagina === 1) {
        pedidos.value = nuevos;
      } else {
        const idsExistentes = new Set(pedidos.value.map((p) => p.id));
        const unicos = nuevos.filter((p) => !idsExistentes.has(p.id));
        pedidos.value = [...pedidos.value, ...unicos];
      }
    } catch (err) {
      error.value = err.message || 'Error al conectar con la API de pedidos.';
      if (resetear) pedidos.value = [];
    } finally {
      cargando.value = false;
      if (event && event.target && event.target.complete) {
        event.target.complete();
      }
    }
  }

  async function cargar_mas() {
    if (cargando.value || !pagina.value.hay_mas) return;
    pagina.value.pagina += 1;
    await cargar(false);
  }

  async function establecer_busqueda(texto) {
    filtros.value.busqueda = texto;
    await cargar(true);
  }

  async function establecer_estados(estadosCsv) {
    filtros.value.estados = estadosCsv;
    await cargar(true);
  }

  async function crear_orden(datos) {
    const res = await pedidos_service.crear(datos);
    await cargar(true);
    return res;
  }

  async function cambiar_estado(id, nuevoEstado, latitud = null, longitud = null) {
    const offline = use_offline_store();
    if (offline.sin_conexion) {
      offline.encolar(id, nuevoEstado, latitud, longitud);
      pedidos.value = pedidos.value.map(con_cambios_sin_enviar);
      return { id, estado: nuevoEstado, sin_enviar: true };
    }
    try {
      const res = await pedidos_service.cambiar_estado(id, nuevoEstado, latitud, longitud);
      await cargar(true);
      return res;
    } catch (err) {
      if (!err.estado_http || err.estado_http === 0) {
        offline.marcar_desconectado();
        offline.encolar(id, nuevoEstado, latitud, longitud);
        pedidos.value = pedidos.value.map(con_cambios_sin_enviar);
        return { id, estado: nuevoEstado, sin_enviar: true };
      }
      throw err;
    }
  }

  async function cancelar_orden(id) {
    const res = await pedidos_service.cancelar(id);
    await cargar(true);
    return res;
  }

    async function actualizar_picking(id, items) {
    const offline = use_offline_store();
    if (offline.sin_conexion) {
      offline.encolar_accion('PICKING_ITEM', id, { items });
      const faltantes = items.filter((i) => i.estado_item === 'EnFalta' || i.cantidad_preparada < i.cantidad_pedida).length;
      const etiquetaPicking = faltantes === 0 ? '[Picking: COMPLETO]' : '[Picking: INCOMPLETO (' + faltantes + ' faltantes)]';
      const pedidoEncontrado = pedidos.value.find((p) => String(p.id) === String(id));
      if (pedidoEncontrado) {
        const obsPrevias = (pedidoEncontrado.observaciones || '').replace(/\[Picking:[^\]]+\]/g, '').trim();
        pedidoEncontrado.observaciones = (obsPrevias + ' ' + etiquetaPicking).trim();
        pedidoEncontrado.sin_enviar = true;
      }
      return {
        id,
        sin_enviar: true,
        completo: faltantes === 0,
        mensaje: 'Picking guardado en el dispositivo. Se sincronizará al volver la red.'
      };
    }
    try {
      const res = await pedidos_service.actualizar_picking(id, items);
      await cargar(true);
      return res;
    } catch (err) {
      if (!err.estado_http || err.estado_http === 0) {
        offline.marcar_desconectado();
        return await actualizar_picking(id, items);
      }
      throw err;
    }
  }

  function limpiar_filtros() {
    filtros.value.busqueda = '';
    filtros.value.estados = '';
    pagina.value.pagina = 1;
  }

  function resetear_store() {
    cargando.value = false;
    error.value = null;
    pedidos.value = [];
    conteos_por_estado.value = [];
    pagina.value = {
      pagina: 1,
      tamano: 10,
      total: 0,
      hay_mas: false
    };
    filtros.value.busqueda = '';
    filtros.value.estados = '';
  }

  return {
    cargando,
    error,
    pedidos,
    conteos_por_estado,
    pagina,
    filtros,
    hay_pedidos,
    total,
    mostrados,
    hay_mas,
    cargar,
    cargar_resumen,
    cargar_mas,
    establecer_busqueda,
    establecer_estados,
    crear_orden,
    cambiar_estado,
    cancelar_orden,
    actualizar_picking,
    limpiar_filtros,
    resetear_store
  };
});

// Proxy retrocompatible para preservar imports directos existentes
export const pedidos_store = new Proxy({}, {
  get(_, prop) {
    const store = usePedidosStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = usePedidosStore();
    store[prop] = val;
    return true;
  }
});
