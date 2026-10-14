import { ref, computed } from 'vue';
import { pedidos_store } from '@/stores/pedidos_store';
import { sesion_store } from '@/stores/sesion_store';
import { pedidos_service } from '@/services/pedidos_service';
import { compartir_archivo } from '@/services/compartir_service';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { geolocalizacion_service } from '@/services/geolocalizacion_service';
import { notificaciones_service } from '@/services/notificaciones_service';

export function usePedidosWorkflow(toast) {
  const mostrarActionSheet = ref(false);
  const mostrarModalComprobante = ref(false);
  const mostrarModalPicking = ref(false);
  const mostrarModalNuevoPedido = ref(false);
  const mostrarModalDetalle = ref(false);
  const mostrarModalCobro = ref(false);
  const pedidoSeleccionado = ref(null);

  const formatear_nombre_estado = (estado) => {
    switch (estado) {
      case 'EnPreparacion': return 'En Depósito';
      case 'Despachado': return 'En Ruta';
      default: return estado;
    }
  };

  const abrirOpcionesEstado = (pedido) => {
    pedidoSeleccionado.value = pedido;
    if (sesion_store.rol_activo === 'CLIENTE') {
      mostrarModalDetalle.value = true;
    } else {
      mostrarActionSheet.value = true;
    }
  };

  const alTocarPedido = (pedido) => {
    pedidoSeleccionado.value = pedido;
    vibrar_toque();

    if (sesion_store.rol_activo === 'OPERARIO') {
      mostrarModalPicking.value = true;
    } else if (sesion_store.rol_activo === 'CHOFER') {
      mostrarActionSheet.value = true;
    } else {
      mostrarModalDetalle.value = true;
    }
  };

  const alActualizarPedidoDetalle = () => {
    if (toast) toast.mostrarExito('¡Pedido actualizado exitosamente!');
    pedidos_store.cargar(true);
  };

  const alRegistrarCobro = (res) => {
    if (toast) toast.mostrarExito(res?.mensaje || '¡Cobro registrado exitosamente!');
    pedidos_store.cargar(true);
  };

  const alSubirComprobante = () => {
    if (toast) toast.mostrarExito('¡Comprobante adjuntado con éxito!');
    pedidos_store.cargar(true);
  };

  const alCompletarPicking = (res) => {
    if (toast) toast.mostrar(res?.mensaje || 'Picking registrado con éxito.', res?.completo ? 'success' : 'warning');
    pedidos_store.cargar(true);
  };

  const alCrearPedidoAdmin = (res) => {
    if (toast) toast.mostrarExito(`¡Pedido ${res?.numero || ''} creado con éxito!`);
    pedidos_store.cargar(true);
  };

  const abrirModalComprobante = (pedido) => {
    pedidoSeleccionado.value = pedido;
    mostrarActionSheet.value = false;
    mostrarModalComprobante.value = true;
  };

  const abrirModalPicking = (pedido) => {
    pedidoSeleccionado.value = pedido;
    mostrarActionSheet.value = false;
    mostrarModalPicking.value = true;
  };

  const compartirComprobante = async (pedido) => {
    mostrarActionSheet.value = false;
    if (!pedido) return;

    try {
      if (toast) toast.mostrarInfo('Generando comprobante oficial PDF con QR…');

      const blob = await pedidos_service.descargar_comprobante(pedido.id);
      const resultado = await compartir_archivo({
        nombre_archivo: `${pedido.numero}.pdf`,
        blob: blob,
        titulo: `Comprobante ${pedido.numero}`,
        texto: `Comprobante oficial de orden ${pedido.numero} de MercuryGO.`
      });

      if (resultado.ok) {
        vibrar_exito();
        if (resultado.mensaje && toast) {
          toast.mostrarExito(resultado.mensaje);
        }
      } else {
        vibrar_error();
        if (toast) toast.mostrarAdvertencia(resultado.mensaje || 'No se pudo compartir el archivo.');
      }
    } catch (err) {
      vibrar_error();
      if (toast) toast.mostrarError(err.message || 'Error al generar el comprobante PDF.');
    }
  };

  const ejecutarCambioEstado = async (nuevoEstado) => {
    if (!pedidoSeleccionado.value) return;
    try {
      let lat = null;
      let lng = null;

      if (nuevoEstado === 'EnPuerta' || nuevoEstado === 'Entregado') {
        try {
          if (toast) toast.mostrarInfo('📍 Obteniendo ubicación GPS del repartidor...');
          const pos = await geolocalizacion_service.obtener_posicion_actual();
          lat = pos.latitud;
          lng = pos.longitud;
        } catch (gpsError) {
          console.warn('No se pudo obtener GPS, procediendo sin coordenadas:', gpsError);
        }
      }

      await pedidos_store.cambiar_estado(pedidoSeleccionado.value.id, nuevoEstado, lat, lng);
      vibrar_exito();

      if (nuevoEstado === 'EnPuerta') {
        await notificaciones_service.disparar_notificacion_local(
          `🚚 En Puerta (${pedidoSeleccionado.value.numero})`,
          `Ubicación registrada. Chofer arribó al domicilio.`
        );
      } else if (nuevoEstado === 'Despachado') {
        await notificaciones_service.disparar_notificacion_local(
          `📦 En Ruta (${pedidoSeleccionado.value.numero})`,
          `Pedido despachado hacia el cliente.`
        );
      } else {
        await notificaciones_service.disparar_notificacion_local(
          `📋 Pedido ${pedidoSeleccionado.value.numero}`,
          `Estado actualizado a: ${formatear_nombre_estado(nuevoEstado)}`
        );
      }

      if (toast) {
        toast.mostrarExito(
          `Orden ${pedidoSeleccionado.value.numero} actualizada a '${formatear_nombre_estado(nuevoEstado)}'.` +
          (lat ? ' (Ubicación GPS guardada)' : '')
        );
      }

      if (nuevoEstado === 'EnPuerta' && lat && lng) {
        setTimeout(() => {
          geolocalizacion_service.abrir_en_google_maps(lat, lng);
        }, 700);
      }
    } catch (err) {
      vibrar_error();
      if (toast) toast.mostrarError(err.message || 'La API rechazó el cambio de estado.');
    } finally {
      mostrarActionSheet.value = false;
    }
  };

  const botonesTransicion = computed(() => {
    if (!pedidoSeleccionado.value) return [];
    const estadoActual = pedidoSeleccionado.value.estado;
    const botones = [];

    botones.push({
      text: '🔍 Ver Detalle Completo de la Orden',
      handler: () => {
        mostrarActionSheet.value = false;
        mostrarModalDetalle.value = true;
      }
    });

    botones.push({
      text: '📄 Ver / Compartir Comprobante PDF (con QR)',
      handler: () => compartirComprobante(pedidoSeleccionado.value)
    });

    const esChoferOAdmin = sesion_store.rol_activo === 'CHOFER' || sesion_store.es_admin;
    if (esChoferOAdmin && (estadoActual === 'Despachado' || estadoActual === 'EnPuerta')) {
      botones.push({
        text: '💵 Registrar Cobro en Destino (Efectivo / QR)',
        handler: () => {
          mostrarActionSheet.value = false;
          mostrarModalCobro.value = true;
        }
      });
    }

    if (estadoActual === 'Pendiente' || estadoActual === 'Confirmado') {
      botones.push({
        text: '💳 Adjuntar Comprobante de Pago (Foto / PDF)',
        handler: () => abrirModalComprobante(pedidoSeleccionado.value)
      });
    }

    const puedePicking = !sesion_store.rol_activo || sesion_store.rol_activo === 'ADMIN' || sesion_store.rol_activo === 'OPERARIO';
    if (puedePicking && (estadoActual === 'Confirmado' || estadoActual === 'EnPreparacion')) {
      botones.push({
        text: '📦 Realizar Picking / Preparación en Depósito',
        handler: () => abrirModalPicking(pedidoSeleccionado.value)
      });
    }

    const esCliente = sesion_store.rol_activo === 'CLIENTE';
    if (esCliente) {
      if (estadoActual === 'Pendiente' || estadoActual === 'Confirmado') {
        botones.push({
          text: 'Cancelar mi Pedido (✖ Cancelar)',
          role: 'destructive',
          handler: () => ejecutarCambioEstado('Cancelado')
        });
      }
      botones.push({
        text: 'Cerrar',
        role: 'cancel'
      });
      return botones;
    }

    if (estadoActual === 'Pendiente') {
      botones.push({
        text: 'Confirmar Pedido (▶ Confirmado)',
        handler: () => ejecutarCambioEstado('Confirmado')
      });
      botones.push({
        text: 'Cancelar Pedido (✖ Cancelado)',
        role: 'destructive',
        handler: () => ejecutarCambioEstado('Cancelado')
      });
    } else if (estadoActual === 'Confirmado') {
      botones.push({
        text: 'Iniciar Carga en Depósito (▶ En Preparación)',
        handler: () => ejecutarCambioEstado('EnPreparacion')
      });
      botones.push({
        text: 'Cancelar Pedido (✖ Cancelado)',
        role: 'destructive',
        handler: () => ejecutarCambioEstado('Cancelado')
      });
    } else if (estadoActual === 'EnPreparacion') {
      botones.push({
        text: 'Despachar al Camión (▶ En Ruta)',
        handler: () => ejecutarCambioEstado('Despachado')
      });
      botones.push({
        text: 'Cancelar Pedido (✖ Cancelado)',
        role: 'destructive',
        handler: () => ejecutarCambioEstado('Cancelado')
      });
    } else if (estadoActual === 'Despachado') {
      botones.push({
        text: 'Llegando al Destino (▶ En Puerta)',
        handler: () => ejecutarCambioEstado('EnPuerta')
      });
      botones.push({
        text: 'Confirmar Entrega Directa (✔ Entregado)',
        handler: () => ejecutarCambioEstado('Entregado')
      });
      botones.push({
        text: 'Devolución / Cancelación (✖ Cancelado)',
        role: 'destructive',
        handler: () => ejecutarCambioEstado('Cancelado')
      });
    } else if (estadoActual === 'EnPuerta') {
      botones.push({
        text: 'Confirmar Entrega Final (✔ Entregado)',
        handler: () => ejecutarCambioEstado('Entregado')
      });
      botones.push({
        text: 'Devolución / Cancelación (✖ Cancelado)',
        role: 'destructive',
        handler: () => ejecutarCambioEstado('Cancelado')
      });
    }

    botones.push({
      text: 'Cerrar',
      role: 'cancel'
    });

    return botones;
  });

  return {
    mostrarActionSheet,
    mostrarModalComprobante,
    mostrarModalPicking,
    mostrarModalNuevoPedido,
    mostrarModalDetalle,
    mostrarModalCobro,
    pedidoSeleccionado,
    formatear_nombre_estado,
    abrirOpcionesEstado,
    alTocarPedido,
    alActualizarPedidoDetalle,
    alRegistrarCobro,
    alSubirComprobante,
    alCompletarPicking,
    alCrearPedidoAdmin,
    compartirComprobante,
    ejecutarCambioEstado,
    botonesTransicion
  };
}
