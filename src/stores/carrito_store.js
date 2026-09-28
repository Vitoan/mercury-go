import { reactive, computed } from 'vue';
import { pedidos_service } from '@/services/pedidos_service';
import { vibrar_exito, vibrar_toque } from '@/services/vibracion_service';

const CLAVE_CARRITO = 'mercurygo_carrito';

function cargar_carrito_inicial() {
  try {
    const raw = localStorage.getItem(CLAVE_CARRITO);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

const state = reactive({
  items: cargar_carrito_inicial(),
  cliente_id: 1, // Por defecto cliente principal o se sincroniza con el usuario
  observaciones: '',
  enviando: false,
  error: null
});

function guardar_en_storage() {
  localStorage.setItem(CLAVE_CARRITO, JSON.stringify(state.items));
}

const cantidad_total = computed(() =>
  state.items.reduce((acc, item) => acc + item.cantidad, 0)
);

const subtotal_total = computed(() =>
  state.items.reduce((acc, item) => acc + (item.precio * item.cantidad), 0)
);

const hay_items = computed(() => state.items.length > 0);

function agregar_producto(producto, cantidad = 1) {
  const index = state.items.findIndex(i => i.producto_id === producto.id);
  if (index >= 0) {
    state.items[index].cantidad += cantidad;
  } else {
    state.items.push({
      producto_id: producto.id,
      nombre: producto.nombre,
      precio: producto.precio,
      imagen_url: producto.imagen_url || null,
      cantidad: cantidad
    });
  }
  guardar_en_storage();
  vibrar_toque();
}

function cambiar_cantidad(producto_id, nueva_cantidad) {
  const item = state.items.find(i => i.producto_id === producto_id);
  if (!item) return;

  if (nueva_cantidad <= 0) {
    quitar_producto(producto_id);
  } else {
    item.cantidad = nueva_cantidad;
    guardar_en_storage();
    vibrar_toque();
  }
}

function quitar_producto(producto_id) {
  state.items = state.items.filter(i => i.producto_id !== producto_id);
  guardar_en_storage();
  vibrar_toque();
}

function vaciar_carrito() {
  state.items = [];
  state.observaciones = '';
  state.error = null;
  guardar_en_storage();
  vibrar_toque();
}

async function confirmar_pedido(cliente_id_custom = null) {
  if (state.items.length === 0) {
    throw new Error('El carrito está vacío.');
  }

  state.enviando = true;
  state.error = null;

  try {
    const payload = {
      cliente_id: cliente_id_custom || state.cliente_id,
      observaciones: state.observaciones || null,
      items: state.items.map(i => ({
        producto_id: i.producto_id,
        cantidad: i.cantidad
      }))
    };

    const res = await pedidos_service.crear(payload);
    vaciar_carrito();
    vibrar_exito();
    return res;
  } catch (err) {
    state.error = err.message || 'No se pudo confirmar el pedido.';
    throw err;
  } finally {
    state.enviando = false;
  }
}

export const carrito_store = {
  get items() { return state.items; },
  get enviando() { return state.enviando; },
  get error() { return state.error; },
  get observaciones() { return state.observaciones; },
  set observaciones(val) { state.observaciones = val; },
  cantidad_total,
  subtotal_total,
  hay_items,
  agregar_producto,
  cambiar_cantidad,
  quitar_producto,
  vaciar_carrito,
  confirmar_pedido
};
