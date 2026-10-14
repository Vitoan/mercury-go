import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { pedidos_service } from '@/services/pedidos_service';

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

      const nuevos = res.pedidos || [];
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
    const res = await pedidos_service.cambiar_estado(id, nuevoEstado, latitud, longitud);
    await cargar(true);
    return res;
  }

  async function cancelar_orden(id) {
    const res = await pedidos_service.cancelar(id);
    await cargar(true);
    return res;
  }

  async function actualizar_picking(id, items) {
    const res = await pedidos_service.actualizar_picking(id, items);
    await cargar(true);
    return res;
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
