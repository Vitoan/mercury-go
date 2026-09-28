import { reactive, computed } from 'vue';
import { pedidos_service } from '@/services/pedidos_service';

const state = reactive({
  cargando: false,
  error: null,
  pedidos: [],
  conteos_por_estado: [],
  total: 0,
  pagina: {
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  },
  filtros: {
    busqueda: '',
    estados: ''
  }
});

const hay_pedidos = computed(() => state.pedidos.length > 0);
const total = computed(() => state.pagina.total);
const mostrados = computed(() => state.pedidos.length);
const hay_mas = computed(() => state.pagina.hay_mas);

async function cargar_resumen() {
  try {
    const data = await pedidos_service.obtener_resumen();
    state.conteos_por_estado = data.conteos_por_estado || [];
  } catch {
    // Silencioso para no romper la vista si falla el conteo auxiliar
  }
}

async function cargar(resetear = false, event = null) {
  state.cargando = true;
  state.error = null;

  if (resetear) {
    state.pagina.pagina = 1;
  }

  try {
    await cargar_resumen();

    const res = await pedidos_service.obtener_pedidos({
      busqueda: state.filtros.busqueda,
      estados: state.filtros.estados,
      pagina: state.pagina.pagina,
      tamano: state.pagina.tamano
    });

    const nuevos = res.pedidos || [];
    state.pagina = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

    if (resetear || state.pagina.pagina === 1) {
      state.pedidos = nuevos;
    } else {
      const idsExistentes = new Set(state.pedidos.map((p) => p.id));
      const unicos = nuevos.filter((p) => !idsExistentes.has(p.id));
      state.pedidos = [...state.pedidos, ...unicos];
    }
  } catch (err) {
    state.error = err.message || 'Error al conectar con la API de pedidos.';
    if (resetear) state.pedidos = [];
  } finally {
    state.cargando = false;
    if (event && event.target && event.target.complete) {
      event.target.complete();
    }
  }
}

async function cargar_mas() {
  if (state.cargando || !state.pagina.hay_mas) return;
  state.pagina.pagina += 1;
  await cargar(false);
}

async function establecer_busqueda(texto) {
  state.filtros.busqueda = texto;
  await cargar(true);
}

async function establecer_estados(estadosCsv) {
  state.filtros.estados = estadosCsv;
  await cargar(true);
}

async function crear_orden(datos) {
  const res = await pedidos_service.crear(datos);
  await cargar(true);
  return res;
}

async function cambiar_estado(id, nuevoEstado) {
  const res = await pedidos_service.cambiar_estado(id, nuevoEstado);
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

export const pedidos_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get pedidos() { return state.pedidos; },
  get conteos_por_estado() { return state.conteos_por_estado; },
  get filtros() { return state.filtros; },
  total,
  mostrados,
  hay_mas,
  hay_pedidos,
  cargar,
  cargar_mas,
  establecer_busqueda,
  establecer_estados,
  crear_orden,
  cambiar_estado,
  cancelar_orden,
  actualizar_picking
};
