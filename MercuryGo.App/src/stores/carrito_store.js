import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { pedidos_service } from '@/services/pedidos_service';
import { vibrar_exito, vibrar_toque } from '@/services/vibracion_service';
import { notificaciones_service } from '@/services/notificaciones_service';
import { use_offline_store } from '@/stores/offline_store';
import { nueva_clave_idempotencia } from '@/utils/idempotencia';

const CLAVE_CARRITO = 'mercurygo_carrito';
const CLAVE_DUENO = 'mercurygo_carrito_dueno';

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
  // El cliente sale de la sesión (sesion_store lo sincroniza). Nunca un id fijo.
  const cliente_id = ref(null);
  const observaciones = ref('');
  const enviando = ref(false);
  const error = ref(null);

  function sincronizar_cliente(id) {
    const nuevo = id ? Number(id) : null;

    // El carrito guardado pertenece a quien lo armó. Si cambió el cliente en este
    // dispositivo (otro usuario sin pasar por logout), se descarta.
    let dueno = null;
    try {
      const guardado = localStorage.getItem(CLAVE_DUENO);
      dueno = guardado ? Number(guardado) : null;
    } catch {
      dueno = null;
    }

    if (nuevo !== null && dueno !== null && dueno !== nuevo && items.value.length > 0) {
      items.value = [];
      observaciones.value = '';
      guardar_en_storage();
    }

    try {
      if (nuevo !== null) {
        localStorage.setItem(CLAVE_DUENO, String(nuevo));
      } else {
        localStorage.removeItem(CLAVE_DUENO);
      }
    } catch {
      // Sin storage disponible (modo privado)
    }

    cliente_id.value = nuevo;
  }

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

    if (enviando.value) {
      throw new Error('El pedido ya se está enviando.');
    }

    // Para el autoservicio el cliente lo define el servidor desde la sesión.
    // El id local solo lo usa el Admin, que sí elige a nombre de quién crea.
    const destino = cliente_id_custom || cliente_id.value;
    if (!destino) {
      throw new Error('Tu usuario no está vinculado a un cliente comercial.');
    }

    enviando.value = true;
    error.value = null;

    try {
      const payload = {
        cliente_id: destino,
        observaciones: observaciones.value || null,
        items: items.value.map(i => ({
          producto_id: i.producto_id,
          cantidad: i.cantidad
        }))
      };

      const res = await pedidos_service.crear(payload, nueva_clave_idempotencia());
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
    sincronizar_cliente,
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
