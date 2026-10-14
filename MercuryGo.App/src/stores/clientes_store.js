import { reactive, computed } from 'vue';
import { clientes_service } from '@/services/clientes_service';

const state = reactive({
  cargando: false,
  error: null,
  clientes: [],
  pagina: {
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  },
  filtros: {
    busqueda: ''
  }
});

const hay_clientes = computed(() => state.clientes.length > 0);
const total = computed(() => state.pagina.total);
const mostrados = computed(() => state.clientes.length);
const hay_mas = computed(() => state.pagina.hay_mas);

async function cargar(resetear = false, event = null) {
  state.cargando = true;
  state.error = null;

  if (resetear) {
    state.pagina.pagina = 1;
  }

  try {
    const res = await clientes_service.obtener_clientes({
      busqueda: state.filtros.busqueda,
      pagina: state.pagina.pagina,
      tamano: state.pagina.tamano
    });

    const nuevos = res.clientes || [];
    state.pagina = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

    if (resetear || state.pagina.pagina === 1) {
      state.clientes = nuevos;
    } else {
      // Acumular páginas descartando duplicados por si se intercaló un alta
      const idsExistentes = new Set(state.clientes.map((c) => c.id));
      const unicos = nuevos.filter((c) => !idsExistentes.has(c.id));
      state.clientes = [...state.clientes, ...unicos];
    }
  } catch (err) {
    state.error = err.message || 'Error al conectar con la API de clientes.';
    if (resetear) state.clientes = [];
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

// Al guardar o editar, siempre se recarga desde la API (Axioma: la pantalla es una vista vieja).
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

export const clientes_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get clientes() { return state.clientes; },
  get filtros() { return state.filtros; },
  total,
  mostrados,
  hay_mas,
  hay_clientes,
  cargar,
  cargar_mas,
  establecer_busqueda,
  guardar_cliente,
  eliminar_cliente
};
