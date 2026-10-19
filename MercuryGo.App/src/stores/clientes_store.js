import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { clientes_service } from '@/services/clientes_service';

export const useClientesStore = defineStore('clientes', () => {
  const cargando = ref(false);
  const error = ref(null);
  const clientes = ref([]);
  const pagina = ref({
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  });
  const filtros = ref({
    busqueda: ''
  });

  const hay_clientes = computed(() => clientes.value.length > 0);
  const total = computed(() => pagina.value.total);
  const mostrados = computed(() => clientes.value.length);
  const hay_mas = computed(() => pagina.value.hay_mas);

  async function cargar(resetear = false, event = null) {
    cargando.value = true;
    error.value = null;

    if (resetear) {
      pagina.value.pagina = 1;
    }

    try {
      const res = await clientes_service.obtener_clientes({
        busqueda: filtros.value.busqueda,
        pagina: pagina.value.pagina,
        tamano: pagina.value.tamano
      });

      const nuevos = res.clientes || [];
      pagina.value = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

      if (resetear || pagina.value.pagina === 1) {
        clientes.value = nuevos;
      } else {
        const idsExistentes = new Set(clientes.value.map((c) => c.id));
        const unicos = nuevos.filter((c) => !idsExistentes.has(c.id));
        clientes.value = [...clientes.value, ...unicos];
      }
    } catch (err) {
      error.value = err.message || 'Error al conectar con la API de clientes.';
      if (resetear) clientes.value = [];
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

  async function guardar_cliente(datos, id = null) {
    if (id) {
      await clientes_service.actualizar(id, datos);
    } else {
      await clientes_service.crear(datos);
    }
    await cargar(true);
  }

  async function eliminar_cliente(id) {
    await clientes_service.eliminar(id);
    await cargar(true);
  }

  return {
    cargando,
    error,
    clientes,
    pagina,
    filtros,
    hay_clientes,
    total,
    mostrados,
    hay_mas,
    cargar,
    cargar_mas,
    establecer_busqueda,
    guardar_cliente,
    eliminar_cliente
  };
});

// Proxy retrocompatible
export const clientes_store = new Proxy({}, {
  get(_, prop) {
    const store = useClientesStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = useClientesStore();
    store[prop] = val;
    return true;
  }
});
