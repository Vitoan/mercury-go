import { reactive, computed } from 'vue';
import { productos_service } from '@/services/productos_service';

const state = reactive({
  cargando: false,
  error: null,
  productos: [],
  categorias: [],
  resumen: {
    total: 0,
    disponibles: 0,
    no_disponibles: 0
  }
});

const hay_productos = computed(() => state.productos.length > 0);

async function cargar(event = null) {
  state.cargando = true;
  state.error = null;

  try {
    const data = await productos_service.obtener_resumen();
    state.productos = data.productos || [];
    state.categorias = data.categorias || [];
    state.resumen = data.resumen || { total: 0, disponibles: 0, no_disponibles: 0 };
  } catch (err) {
    state.error = err.message || 'Ocurrió un error al consultar el catálogo de productos.';
    state.productos = [];
  } finally {
    state.cargando = false;
    if (event && event.target && event.target.complete) {
      event.target.complete();
    }
  }
}

export const productos_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get productos() { return state.productos; },
  get categorias() { return state.categorias; },
  get resumen() { return state.resumen; },
  hay_productos,
  cargar
};
