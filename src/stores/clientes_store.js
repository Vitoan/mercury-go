import { reactive, computed } from 'vue';
import { clientes_service } from '@/services/clientes_service';

const state = reactive({
  cargando: false,
  error: null,
  clientes: [],
  total: 0
});

const hay_clientes = computed(() => state.clientes.length > 0);

async function cargar(event = null) {
  state.cargando = true;
  state.error = null;

  try {
    const data = await clientes_service.obtener_clientes();
    state.clientes = data.clientes || [];
    state.total = data.total || 0;
  } catch (err) {
    state.error = err.message || 'Ocurrió un error al consultar los clientes.';
    state.clientes = [];
  } finally {
    state.cargando = false;
    if (event && event.target && event.target.complete) {
      event.target.complete();
    }
  }
}

export const clientes_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get clientes() { return state.clientes; },
  get total() { return state.total; },
  hay_clientes,
  cargar
};
