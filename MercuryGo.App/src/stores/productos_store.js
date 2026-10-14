import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { productos_service } from '@/services/productos_service';

export const useProductosStore = defineStore('productos', () => {
  const cargando = ref(false);
  const error = ref(null);
  const productos = ref([]);
  const categorias = ref([]);
  const ofertas = ref([]);
  const resumen = ref({
    total: 0,
    disponibles: 0,
    no_disponibles: 0
  });
  const pagina = ref({
    pagina: 1,
    tamano: 10,
    total: 0,
    hay_mas: false
  });
  const filtros = ref({
    busqueda: '',
    categoria_id: null,
    disponible: null,
    en_oferta: null
  });

  const hay_productos = computed(() => productos.value.length > 0);
  const total = computed(() => pagina.value.total);
  const mostrados = computed(() => productos.value.length);
  const hay_mas = computed(() => pagina.value.hay_mas);

  // Carga la vitrina completa sin paginar (para inicio o resumen)
  async function cargar_resumen(event = null) {
    cargando.value = true;
    error.value = null;

    try {
      const data = await productos_service.obtener_resumen();
      productos.value = data.productos || [];
      categorias.value = data.categorias || [];
      ofertas.value = data.ofertas || (data.productos ? data.productos.filter(p => p.en_oferta) : []);
      resumen.value = data.resumen || { total: 0, disponibles: 0, no_disponibles: 0 };
      pagina.value = {
        pagina: 1,
        tamano: productos.value.length,
        total: productos.value.length,
        hay_mas: false
      };
    } catch (err) {
      error.value = err.message || 'Ocurrió un error al consultar el catálogo de productos.';
      productos.value = [];
    } finally {
      cargando.value = false;
      if (event && event.target && event.target.complete) {
        event.target.complete();
      }
    }
  }

  // Carga el catálogo paginado con filtros en servidor
  async function cargar_listado(resetear = false, event = null) {
    cargando.value = true;
    error.value = null;

    if (resetear) {
      pagina.value.pagina = 1;
    }

    try {
      const res = await productos_service.obtener_listado({
        busqueda: filtros.value.busqueda,
        categoria_id: filtros.value.categoria_id,
        disponible: filtros.value.disponible,
        en_oferta: filtros.value.en_oferta,
        pagina: pagina.value.pagina,
        tamano: pagina.value.tamano
      });

      const nuevos = res.productos || [];
      pagina.value = res.pagina || { pagina: 1, tamano: 10, total: nuevos.length, hay_mas: false };

      if (resetear || pagina.value.pagina === 1) {
        productos.value = nuevos;
      } else {
        const idsExistentes = new Set(productos.value.map((p) => p.id));
        const unicos = nuevos.filter((p) => !idsExistentes.has(p.id));
        productos.value = [...productos.value, ...unicos];
      }
    } catch (err) {
      error.value = err.message || 'Error al conectar con el catálogo de productos.';
      if (resetear) productos.value = [];
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
    await cargar_listado(false);
  }

  async function establecer_busqueda(texto) {
    filtros.value.busqueda = texto;
    await cargar_listado(true);
  }

  async function establecer_categoria(categoriaId) {
    filtros.value.categoria_id = categoriaId;
    await cargar_listado(true);
  }

  async function establecer_disponible(disponible) {
    filtros.value.disponible = disponible;
    await cargar_listado(true);
  }

  async function establecer_oferta(enOferta) {
    filtros.value.en_oferta = enOferta;
    await cargar_listado(true);
  }

  async function guardar_producto(datos, id = null) {
    if (id) {
      await productos_service.actualizar(id, datos);
    } else {
      await productos_service.crear(datos);
    }
    await Promise.all([cargar_resumen(), cargar_listado(true)]);
  }

  async function eliminar_producto(id) {
    await productos_service.eliminar(id);
    await Promise.all([cargar_resumen(), cargar_listado(true)]);
  }

  return {
    cargando,
    error,
    productos,
    categorias,
    ofertas,
    resumen,
    filtros,
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
    establecer_oferta,
    guardar_producto,
    eliminar_producto
  };
});

// Proxy retrocompatible
export const productos_store = new Proxy({}, {
  get(_, prop) {
    const store = useProductosStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = useProductosStore();
    store[prop] = val;
    return true;
  }
});
