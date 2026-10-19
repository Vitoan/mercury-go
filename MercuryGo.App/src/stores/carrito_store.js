import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { pedidos_service } from '@/services/pedidos_service';
import { vibrar_exito, vibrar_toque } from '@/services/vibracion_service';
import { notificaciones_service } from '@/services/notificaciones_service';

const CLAVE_CARRITO = 'mercurygo_carrito';

function cargar_carrito_inicial() {
  try {
    const raw = localStorage.getItem(CLAVE_CARRITO);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

export const useCarritoStore = defineStore('carrito', () => {
  const items = ref(cargar_carrito_inicial());
  const cliente_id = ref(1); // Por defecto cliente principal o se sincroniza con el usuario
  const observaciones = ref('');
  const enviando = ref(false);
  const error = ref(null);

  function guardar_en_storage() {
    localStorage.setItem(CLAVE_CARRITO, JSON.stringify(items.value));
  }

  const cantidad_total = computed(() =>
    items.value.reduce((acc, item) => acc + item.cantidad, 0)
  );

  const subtotal_total = computed(() =>
    items.value.reduce((acc, item) => acc + ((Number(item.precio) || 0) * (Number(item.cantidad) || 0)), 0)
  );

  const hay_items = computed(() => items.value.length > 0);

  function agregar_producto(producto, cantidad = 1) {
    const index = items.value.findIndex(i => i.producto_id === producto.id);
    if (index >= 0) {
      items.value[index].cantidad += cantidad;
    } else {
      items.value.push({
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
    const item = items.value.find(i => i.producto_id === producto_id);
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
    items.value = items.value.filter(i => i.producto_id !== producto_id);
    guardar_en_storage();
    vibrar_toque();
  }

  function vaciar_carrito() {
    items.value = [];
    observaciones.value = '';
    error.value = null;
    guardar_en_storage();
    vibrar_toque();
  }

  async function confirmar_pedido(cliente_id_custom = null) {
    if (items.value.length === 0) {
      throw new Error('El carrito está vacío.');
    }

    enviando.value = true;
    error.value = null;

    try {
      const payload = {
        cliente_id: cliente_id_custom || cliente_id.value,
        observaciones: observaciones.value || null,
        items: items.value.map(i => ({
          producto_id: i.producto_id,
          cantidad: i.cantidad
        }))
      };

      const res = await pedidos_service.crear(payload);
      vaciar_carrito();
      vibrar_exito();

      try {
        const pedidoInfo = res?.pedido || res || {};
        const numPedido = pedidoInfo.numero || 'Nuevo';
        await notificaciones_service.disparar_notificacion_local(
          'Pedido Confirmado 📦',
          `Orden ${numPedido} creada exitosamente en el sistema.`
        );
      } catch (notifErr) {
        console.warn('[Carrito] Error al emitir notificacion:', notifErr);
      }

      return res;
    } catch (err) {
      error.value = err.message || 'No se pudo confirmar el pedido.';
      throw err;
    } finally {
      enviando.value = false;
    }
  }

  return {
    items,
    cliente_id,
    observaciones,
    enviando,
    error,
    cantidad_total,
    subtotal_total,
    hay_items,
    agregar_producto,
    cambiar_cantidad,
    quitar_producto,
    vaciar_carrito,
    confirmar_pedido
  };
});

// Proxy retrocompatible
export const carrito_store = new Proxy({}, {
  get(_, prop) {
    const store = useCarritoStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = useCarritoStore();
    store[prop] = val;
    return true;
  }
});
