import { defineStore } from 'pinia';
import { Network } from '@capacitor/network';
import { pedidos_service } from '@/services/pedidos_service';
import { obtener_token } from '@/services/token_service';
import { usePedidosStore } from '@/stores/pedidos_store';
import { vibrar_exito, vibrar_advertencia } from '@/services/vibracion_service';

const clave_cola = 'mercurygo_cola_offline';

export const use_offline_store = defineStore('offline', {
  state: () => ({
    en_linea: typeof navigator !== 'undefined' ? navigator.onLine : true,
    cola: JSON.parse(localStorage.getItem(clave_cola) || '[]'),
    sincronizando: false
  }),
  getters: {
    sin_conexion(state) {
      return !state.en_linea;
    }
  },
  actions: {
    iniciar: async function () {
      const vm = this;
      if (typeof window !== 'undefined') {
        window.addEventListener('online', () => {
          vm.marcar_en_linea();
        });
        window.addEventListener('offline', () => {
          vm.marcar_desconectado();
        });
      }
      try {
        Network.addListener('networkStatusChange', function (estado) {
          if (estado.connected) {
            vm.marcar_en_linea();
          } else {
            vm.marcar_desconectado();
          }
        });
        const estado = await Network.getStatus();
        if (estado.connected) {
          vm.marcar_en_linea();
        } else {
          vm.marcar_desconectado();
        }
      } catch (err) {
        console.warn('No se pudo inicializar Network listener:', err);
      }
    },
    marcar_desconectado: function () {
      const vm = this;
      vm.en_linea = false;
    },
    marcar_en_linea: function () {
      const vm = this;
      const estabaDesconectado = !vm.en_linea;
      vm.en_linea = true;
      if (estabaDesconectado || vm.cola.length > 0) {
        vm.sincronizar();
      }
    },
    guardar_cola: function (cola) {
      const vm = this;
      vm.cola = cola;
      try {
        localStorage.setItem(clave_cola, JSON.stringify(cola));
      } catch (error) {
        console.warn('Error al guardar cola offline:', error);
      }
    },
    encolar: function (pedido_id, estado, latitud = null, longitud = null) {
      const vm = this;
      const nuevo = {
        id: Date.now(),
        pedido_id,
        estado,
        latitud,
        longitud
      };
      vm.guardar_cola([...vm.cola, nuevo]);
    },
    sincronizar: async function () {
      const vm = this;
      const token = await obtener_token();
      if (vm.sincronizando || vm.cola.length === 0 || !token) return;

      vm.sincronizando = true;
      const en_cola = vm.cola.length;
      const rechazados = [];

      for (const cambio of [...vm.cola]) {
        try {
          await pedidos_service.cambiar_estado(
            cambio.pedido_id,
            cambio.estado,
            cambio.latitud,
            cambio.longitud
          );
        } catch (error) {
          const estadoHttp = error?.estado_http;
          // Sin red, sesión vencida o servidor caído: sigue en la cola
          if (!estadoHttp || estadoHttp === 401 || estadoHttp >= 500) {
            continue;
          }
          // Rechazo 400 o 409: otro usuario movió el pedido mientras no había red, no mejora reintentando
          rechazados.push(error.message || error.mensaje || 'Transición no permitida');
        }
        vm.guardar_cola(vm.cola.filter((item) => item.id !== cambio.id));
      }

      vm.sincronizando = false;

      if (rechazados.length > 0) {
        vibrar_advertencia();
        console.warn(`La API no aceptó ${rechazados.length} cambios guardados:`, rechazados[0]);
      } else if (vm.cola.length < en_cola) {
        vibrar_exito();
      }

      if (vm.cola.length < en_cola) {
        try {
          await usePedidosStore().cargar(true);
        } catch (e) {
          console.warn('Error al recargar pedidos tras sincronización:', e);
        }
      }
    }
  }
});

export const offline_store = new Proxy({}, {
  get(_, prop) {
    const store = use_offline_store();
    return store[prop];
  },
  set(_, prop, val) {
    const store = use_offline_store();
    store[prop] = val;
    return true;
  }
});
