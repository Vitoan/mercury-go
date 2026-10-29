import { defineStore } from 'pinia';
import { Network } from '@capacitor/network';
import { pedidos_service } from '@/services/pedidos_service';
import { obtener_token } from '@/services/token_service';
import { usePedidosStore } from '@/stores/pedidos_store';
import { notificaciones_service } from '@/services/notificaciones_service';
import { vibrar_exito, vibrar_advertencia, vibrar_error } from '@/services/vibracion_service';


function base64AArchivo(base64Data, nombreArchivo = 'comprobante.jpg', mimeType = 'image/jpeg') {
  try {
    const arr = base64Data.split(',');
    const mime = (arr[0].match(/:(.*?);/) || [])[1] || mimeType;
    const bstr = atob(arr.length > 1 ? arr[1] : arr[0]);
    let n = bstr.length;
    const u8arr = new Uint8Array(n);
    while (n--) {
      u8arr[n] = bstr.charCodeAt(n);
    }
    return new File([u8arr], nombreArchivo, { type: mime });
  } catch (err) {
    console.error('Error al convertir base64 a archivo:', err);
    return null;
  }
}

const clave_cola = 'mercurygo_cola_offline_v2';

function generar_uuid_v4() {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export const use_offline_store = defineStore('offline', {
  state: () => ({
    en_linea: typeof navigator !== 'undefined' ? navigator.onLine : true,
    cola: JSON.parse(localStorage.getItem(clave_cola) || '[]'),
    sincronizando: false
  }),

  getters: {
    sin_conexion(state) {
      return !state.en_linea;
    },
    pendientes_count(state) {
      return state.cola.filter((a) => a.estado === 'pendiente' || a.estado === 'enviando' || a.estado === 'error').length;
    },
    conflictos_count(state) {
      return state.cola.filter((a) => a.estado === 'conflicto').length;
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
        Network.addListener('networkStatusChange', (estado) => {
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
      this.en_linea = false;
    },

    marcar_en_linea: function () {
      const estabaDesconectado = !this.en_linea;
      this.en_linea = true;
      if (estabaDesconectado || this.pendientes_count > 0) {
        this.sincronizar();
      }
    },

    guardar_cola: function (nuevaCola) {
      this.cola = nuevaCola;
      try {
        localStorage.setItem(clave_cola, JSON.stringify(nuevaCola));
      } catch (error) {
        console.warn('Error al guardar cola offline:', error);
      }
    },

    // Encola una acción con UUID v4 e Idempotency-Key
    encolar_accion: function (tipo, pedido_id, payload = {}, lat = null, lng = null, archivo_local = null) {
      const accion = {
        id: generar_uuid_v4(),
        tipo, // ENTREGAR | EN_PUERTA | NO_ENTREGADO | COBRO | PICKING_ITEM | DESPACHAR | CREAR_PEDIDO
        pedido_id: Number(pedido_id),
        payload,
        ocurrido_en: new Date().toISOString(),
        lat,
        lng,
        intentos: 0,
        estado: 'pendiente', // pendiente | enviando | enviada | error | conflicto
        error: null,
        archivo_local
      };

      this.guardar_cola([...this.cola, accion]);
      return accion;
    },

    // Compatibilidad retroactiva con la llamada previa de encolar estado
    encolar: function (pedido_id, estado, latitud = null, longitud = null, ocurrido_en = null) {
      const payload = ocurrido_en ? { estado, ocurrido_en } : { estado };
      return this.encolar_accion(estado.toUpperCase(), pedido_id, payload, latitud, longitud);
    },

    limpiar_enviadas: function () {
      this.guardar_cola(this.cola.filter((a) => a.estado !== 'enviada'));
    },

    reintentar_accion: function (accionId) {
      const colaActualizada = this.cola.map((a) => {
        if (a.id === accionId) {
          return { ...a, estado: 'pendiente', error: null };
        }
        return a;
      });
      this.guardar_cola(colaActualizada);
      if (this.en_linea) {
        this.sincronizar();
      }
    },

    // Ejecución FIFO agrupada por pedido_id
    sincronizar: async function () {
      const token = await obtener_token();
      if (this.sincronizando || this.cola.length === 0 || !token) return;

      this.sincronizando = true;
      let exitoAlMenosUno = false;
      let conflictoOcurrido = false;

      // Agrupar acciones pendientes en orden FIFO por pedido_id
      const pedidosIds = [...new Set(this.cola.filter((a) => a.estado === 'pendiente' || a.estado === 'error').map((a) => a.pedido_id))];

      for (const pId of pedidosIds) {
        const accionesDelPedido = this.cola.filter((a) => a.pedido_id === pId && (a.estado === 'pendiente' || a.estado === 'error'));

        for (const accion of accionesDelPedido) {
          if (!this.en_linea) break;

          accion.estado = 'enviando';
          accion.intentos = (accion.intentos || 0) + 1;
          this.guardar_cola([...this.cola]);

          try {
            await this.ejecutar_despacho_api(accion);
            accion.estado = 'enviada';
            accion.error = null;
            exitoAlMenosUno = true;
          } catch (err) {
            const estadoHttp = err?.estado_http;
            const esErrorConexion = !estadoHttp || estadoHttp === 0 || estadoHttp >= 500;

            if (esErrorConexion) {
              accion.estado = 'error';
              accion.error = err?.message || 'Error temporal de conexión.';
              // No continuar con las siguientes acciones de ESTE pedido (garantizar FIFO)
              break;
            } else if (estadoHttp === 409 || estadoHttp === 400) {
              accion.estado = 'conflicto';
              accion.error = err?.mensaje || err?.message || 'Conflicto de estado en servidor.';
              conflictoOcurrido = true;
              break;
            } else {
              accion.estado = 'error';
              accion.error = err?.mensaje || err?.message || 'Rechazado por servidor.';
              break;
            }
          } finally {
            this.guardar_cola([...this.cola]);
          }
        }
      }

      this.sincronizando = false;

      if (conflictoOcurrido) {
        vibrar_advertencia();
      } else if (exitoAlMenosUno) {
        vibrar_exito();
      }

      if (exitoAlMenosUno) {
        try {
          await usePedidosStore().cargar(true);
        } catch {
          // Ignorar recarga fallida
        }
      }
    },

    ejecutar_despacho_api: async function (accion) {
      const { tipo, pedido_id, payload, lat, lng, id, ocurrido_en } = accion;

      switch (tipo) {
        case 'ENTREGAR':
        case 'EN_PUERTA':
        case 'DESPACHADO':
        case 'ENPREPARACION':
        case 'CONFIRMADO':
        case 'CANCELADO': {
          const estadoDestino = payload?.estado || (tipo === 'EN_PUERTA' ? 'EnPuerta' : tipo === 'ENTREGAR' ? 'Entregado' : tipo);
          return await pedidos_service.cambiar_estado(pedido_id, estadoDestino, lat, lng, id, payload?.ocurrido_en || ocurrido_en);
        }

        case 'NO_ENTREGADO': {
          return await pedidos_service.registrar_no_entregado(
            pedido_id,
            payload?.motivo || 'ClienteAusente',
            payload?.nota,
            payload?.foto_url,
            id,
            payload?.ocurrido_en || ocurrido_en
          );
        }

        case 'COBRO': {
          return await pedidos_service.registrar_cobro_chofer(
            pedido_id,
            payload?.monto,
            payload?.metodo || 'Efectivo',
            id,
            payload?.ocurrido_en
          );
        }

        case 'ENTREGA_PARCIAL': {
          return await pedidos_service.registrar_entrega_parcial(
            pedido_id,
            payload?.lineas || [],
            payload?.motivo,
            lat,
            lng,
            payload?.foto_url,
            payload?.firma_url,
            id,
            payload?.ocurrido_en || ocurrido_en
          );
        }

        
        case 'COMPROBANTE_PAGO': {
          let archivo;
          if (payload && payload.base64) {
            archivo = base64AArchivo(payload.base64, payload.nombre || 'comprobante.jpg', payload.tipo || 'image/jpeg');
          }
          if (!archivo) throw new Error('Archivo de comprobante no disponible en la cola offline.');
          const res = await pedidos_service.subir_comprobante_pago(pedido_id, archivo);
          try {
            await notificaciones_service.disparar_notificacion_local(
              '✅ Comprobante Sincronizado',
              'El comprobante de pago fue recibido por el servidor.'
            );
          } catch (notifErr) {
            console.warn('[Offline] Error al disparar notificación de comprobante:', notifErr);
          }
          return res;
        }

        case 'PICKING_ITEM': {
          return await pedidos_service.actualizar_picking(pedido_id, payload?.items || [], id);
        }

        case 'CREAR_PEDIDO': {
          const resultado = await pedidos_service.crear(payload, id);
          try {
            const pedidoCreado = resultado?.pedido || resultado || {};
            const num = pedidoCreado.numero || 'Generado';
            await notificaciones_service.disparar_notificacion_local(
              '✅ Pedido Sincronizado',
              'Tu orden ' + num + ' creada fuera de línea ya fue confirmada por el servidor.'
            );
          } catch (notifErr) {
            console.warn('[Offline] Error al disparar notificación de pedido sincronizado:', notifErr);
          }
          return resultado;
        }

        default: {
          if (payload?.estado) {
            return await pedidos_service.cambiar_estado(pedido_id, payload.estado, lat, lng, id, payload?.ocurrido_en || ocurrido_en);
          }
          throw new Error(`Tipo de acción no soportado: ${tipo}`);
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
