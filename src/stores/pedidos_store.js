import { reactive, computed } from 'vue';
import { pedidos_service } from '@/services/pedidos_service';

const state = reactive({
  cargando: false,
  error: null,
  pedidos: [],
  conteos_por_estado: [],
  total: 0
});

const hay_pedidos = computed(() => state.pedidos.length > 0);

async function cargar(event = null) {
  state.cargando = true;
  state.error = null;

  try {
    const data = await pedidos_service.obtener_resumen();
    state.pedidos = data.pedidos || [];
    state.conteos_por_estado = data.conteos_por_estado || [];
    state.total = data.total || 0;
  } catch (err) {
    state.error = err.message || 'Ocurrió un error al consultar los pedidos.';
    state.pedidos = [];
  } finally {
    state.cargando = false;
    if (event && event.target && event.target.complete) {
      event.target.complete();
    }
  }
}

export const pedidos_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get pedidos() { return state.pedidos; },
  get conteos_por_estado() { return state.conteos_por_estado; },
  get total() { return state.total; },
  hay_pedidos,
  cargar
};
