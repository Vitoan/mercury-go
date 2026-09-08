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
  },
  pagina: {
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  },
  filtros: {
    busqueda: '',
    categoria_id: null,
    disponible: null
  }
});

const hay_productos = computed(() => state.productos.length > 0);
const total = computed(() => state.pagina.total);
const mostrados = computed(() => state.productos.length);
const hay_mas = computed(() => state.pagina.hay_mas);

// Carga la vitrina completa sin paginar (para inicio o resumen)
async function cargar_resumen(event = null) {
  state.cargando = true;
  state.error = null;

  try {
    const data = await productos_service.obtener_resumen();
    state.productos = data.productos || [];
    state.categorias = data.categorias || [];
    state.resumen = data.resumen || { total: 0, disponibles: 0, no_disponibles: 0 };
    state.pagina = {
      pagina: 1,
      tamano: state.productos.length,
      total: state.productos.length,
      hay_mas: false
    };
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

// Carga el catálogo paginado con filtros en servidor
async function cargar_listado(resetear = false, event = null) {
  state.cargando = true;
  state.error = null;

  if (resetear) {
    state.pagina.pagina = 1;
  }

  try {
    const res = await productos_service.obtener_listado({
      busqueda: state.filtros.busqueda,
      categoria_id: state.filtros.categoria_id,
      disponible: state.filtros.disponible,
      pagina: state.pagina.pagina,
      tamano: state.pagina.tamano
    });

    const nuevos = res.productos || [];
    state.pagina = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

    if (resetear || state.pagina.pagina === 1) {
      state.productos = nuevos;
    } else {
      const idsExistentes = new Set(state.productos.map((p) => p.id));
      const unicos = nuevos.filter((p) => !idsExistentes.has(p.id));
      state.productos = [...state.productos, ...unicos];
    }
  } catch (err) {
    state.error = err.message || 'Error al conectar con el catálogo de productos.';
    if (resetear) state.productos = [];
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
  await cargar_listado(false);
}

async function establecer_busqueda(texto) {
  state.filtros.busqueda = texto;
  await cargar_listado(true);
}

async function establecer_categoria(categoriaId) {
  state.filtros.categoria_id = categoriaId;
  await cargar_listado(true);
}

async function establecer_disponible(disponible) {
  state.filtros.disponible = disponible;
  await cargar_listado(true);
}

async function guardar_producto(datos, id = null) {
  if (id) {
    await productos_service.actualizar(id, datos);
  } else {
    await productos_service.crear(datos);
  }
  await cargar_listado(true);
}

async function eliminar_producto(id) {
  await productos_service.eliminar(id);
  await cargar_listado(true);
}

export const productos_store = {
  get cargando() { return state.cargando; },
  get error() { return state.error; },
  get productos() { return state.productos; },
  get categorias() { return state.categorias; },
  get resumen() { return state.resumen; },
  get filtros() { return state.filtros; },
  total,
  mostrados,
  hay_mas,
  hay_productos,
  cargar: cargar_listado,
  cargar_resumen,
  cargar_listado,
  cargar_mas,
  establecer_busqueda,
  establecer_categoria,
  establecer_disponible,
  guardar_producto,
  eliminar_producto
};
