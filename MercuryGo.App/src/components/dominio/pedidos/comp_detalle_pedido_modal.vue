<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Detalle de Orden de Carga</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="cargando" class="mercury-modal-loading">
        <ion-spinner name="crescent" color="primary" />
        <p>Cargando detalles de la orden…</p>
      </div>

      <div v-else-if="error" class="mercury-modal-error">
        <ion-icon :icon="alertCircleOutline" color="danger" />
        <p>{{ error }}</p>
        <ion-button size="small" fill="outline" @click="cargarDetalle">Reintentar</ion-button>
      </div>

      <div v-else-if="detalle" class="mercury-detalle-contenido">
        <!-- Encabezado con Número, Estado y Acciones Rápidas -->
        <ion-card class="mercury-seccion-card">
          <ion-card-header>
            <div class="mercury-cabecera-fila">
              <div>
                <ion-card-subtitle>Remito Oficial</ion-card-subtitle>
                <ion-card-title class="mercury-numero-orden">{{ detalle.numero }}</ion-card-title>
              </div>
              <div class="mercury-cabecera-badges">
                <ion-badge :color="obtener_color_estado(detalle.estado)" class="mercury-badge-estado-grande">
                  {{ detalle.estado }}
                </ion-badge>
                <ion-badge v-if="detalle.sin_enviar" color="warning" class="mercury-badge-estado-grande">
                  Sin enviar
                </ion-badge>
              </div>
            </div>
          </ion-card-header>
          <ion-card-content>
            <p><strong>Fecha:</strong> {{ formatear_fecha(detalle.fecha_pedido) }}</p>
            <p><strong>Cliente:</strong> {{ detalle.cliente?.razon_social }} (CUIT: {{ detalle.cliente?.cuit }})</p>
            <p v-if="observacionesVisibles"><strong>Observaciones:</strong> {{ observacionesVisibles }}</p>

            <!-- Sección de Geolocalización de Entrega (Semana 7) -->
            <div v-if="detalle.latitud_entrega && detalle.longitud_entrega" class="mercury-gps-seccion">
              <div class="mercury-gps-info">
                <span>📍 <strong>Ubicación registrada:</strong> {{ detalle.latitud_entrega?.toFixed(5) }}, {{ detalle.longitud_entrega?.toFixed(5) }}</span>
                <span v-if="detalle.fecha_en_puerta" class="mercury-gps-fecha">({{ formatear_fecha(detalle.fecha_en_puerta) }})</span>
              </div>
              <ion-button
                size="small"
                fill="outline"
                color="secondary"
                @click="abrirEnMaps(detalle.latitud_entrega, detalle.longitud_entrega)"
              >
                <ion-icon slot="start" :icon="navigateOutline" />
                Ver en Google Maps
              </ion-button>
            </div>

            <!-- Sección de Auditoría de Comprobante Bancario -->
            <div v-if="comprobanteInfo" class="mercury-comprobante-seccion">
              <div class="mercury-comprobante-header">
                <strong>💳 Comprobante de Pago Adjunto</strong>
                <ion-badge :color="comprobanteInfo.color">{{ comprobanteInfo.estadoTexto }}</ion-badge>
              </div>

              <div class="mercury-comprobante-acciones">
                <ion-button
                  v-if="comprobanteInfo.url"
                  size="small"
                  fill="outline"
                  color="tertiary"
                  @click="abrirComprobanteAdjunto"
                >
                  <ion-icon slot="start" :icon="documentAttachOutline" />
                  Ver Comprobante
                </ion-button>

                <!-- Acciones de Aprobación para ADMIN cuando está pendiente -->
                <template v-if="esAdmin && comprobanteInfo.esPendiente">
                  <ion-button
                    size="small"
                    color="success"
                    :disabled="procesandoPago"
                    @click="aprobarComprobante"
                  >
                    <ion-icon slot="start" :icon="checkmarkCircleOutline" />
                    Aprobar Pago
                  </ion-button>

                  <ion-button
                    size="small"
                    color="danger"
                    fill="outline"
                    :disabled="procesandoPago"
                    @click="rechazarComprobante"
                  >
                    <ion-icon slot="start" :icon="closeCircleOutline" />
                    Rechazar
                  </ion-button>
                </template>
              </div>
            </div>

            <div v-if="!offline_store.sin_conexion" class="mercury-botones-pdf-fila">
              <ion-button
                expand="block"
                color="primary"
                fill="outline"
                @click="descargarPdf"
              >
                <ion-icon slot="start" :icon="documentTextOutline" />
                📄 Ver / Compartir Comprobante PDF (con QR)
              </ion-button>
            </div>
          </ion-card-content>
        </ion-card>

        <!-- Artículos del Pedido -->
        <ion-card class="mercury-seccion-card">
          <ion-card-header>
            <div class="mercury-cabecera-fila">
              <ion-card-title>Mercadería Incluida</ion-card-title>
              <ion-button
                v-if="esAdmin && !modoEdicion && !offline_store.sin_conexion && detalle.estado !== 'Entregado' && detalle.estado !== 'Cancelado'"
                size="small"
                fill="clear"
                color="secondary"
                @click="activarEdicion"
              >
                <ion-icon slot="start" :icon="createOutline" />
                Editar Pedido
              </ion-button>
            </div>
          </ion-card-header>
          <ion-card-content>
            <ion-list>
              <ion-item v-for="item in itemsEdicion" :key="item.producto_id" class="mercury-item-linea">
                <ion-icon slot="start" :icon="cubeOutline" color="primary" />
                <ion-label>
                  <h3>{{ item.producto_nombre }}</h3>
                  <p>${{ item.precio_unitario?.toLocaleString('es-AR') }} c/u</p>
                  <p>Subtotal: <strong>${{ (item.precio_unitario * item.cantidad).toLocaleString('es-AR') }}</strong></p>
                </ion-label>

                <!-- Si está en modo edición, mostrar stepper -->
                <div v-if="modoEdicion" slot="end" class="mercury-stepper">
                  <ion-button size="small" fill="clear" color="medium" @click="decrementar(item)">
                    <ion-icon :icon="removeCircleOutline" />
                  </ion-button>
                  <span class="mercury-cant-num">{{ item.cantidad }}</span>
                  <ion-button size="small" fill="clear" color="medium" @click="incrementar(item)">
                    <ion-icon :icon="addCircleOutline" />
                  </ion-button>
                  <ion-button size="small" fill="clear" color="danger" @click="eliminarItem(item)">
                    <ion-icon :icon="trashOutline" />
                  </ion-button>
                </div>
                <div v-else slot="end" class="mercury-cant-badge">
                  <ion-badge color="light">{{ item.cantidad }} u.</ion-badge>
                </div>
              </ion-item>
            </ion-list>

            <div v-if="modoEdicion" class="mercury-edicion-obs">
              <ion-item lines="inset">
                <ion-label position="stacked">Modificar Observaciones</ion-label>
                <ion-textarea v-model="observacionesEdicion" :rows="2" />
              </ion-item>

              <div class="mercury-botones-edicion-acciones">
                <ion-button size="small" color="success" :disabled="guardandoEdicion" @click="guardarCambiosEdicion">
                  <ion-spinner v-if="guardandoEdicion" name="dots" />
                  <span v-else>Guardar Cambios</span>
                </ion-button>
                <ion-button size="small" fill="clear" color="medium" @click="cancelarEdicion">
                  Cancelar
                </ion-button>
              </div>
            </div>

            <div class="mercury-total-fila">
              <span>Total de la Orden:</span>
              <strong class="mercury-total-destacado">${{ totalCalculado.toLocaleString('es-AR') }}</strong>
            </div>
          </ion-card-content>
        </ion-card>
      </div>
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import {
  IonModal,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonButtons,
  IonButton,
  IonContent,
  IonCard,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonCardContent,
  IonList,
  IonItem,
  IonLabel,
  IonBadge,
  IonIcon,
  IonTextarea,
  IonSpinner
} from '@ionic/vue';
import {
  documentTextOutline,
  cubeOutline,
  createOutline,
  addCircleOutline,
  removeCircleOutline,
  trashOutline,
  alertCircleOutline,
  documentAttachOutline,
  checkmarkCircleOutline,
  closeCircleOutline,
  navigateOutline
} from 'ionicons/icons';
import { pedidos_service } from '@/services/pedidos_service';
import { sesion_store } from '@/stores/sesion_store';
import { use_offline_store } from '@/stores/offline_store';
import { con_cambios_sin_enviar } from '@/stores/pedidos_store';
import { compartir_archivo } from '@/services/compartir_service';
import { geolocalizacion_service } from '@/services/geolocalizacion_service';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { resolver_url_imagen } from '@/utils/imagenes';

const offline_store = use_offline_store();

const props = defineProps({
  abierto: { type: Boolean, default: false },
  pedidoId: { type: [Number, String], default: null }
});

const emit = defineEmits(['cerrar', 'pedido_actualizado']);

const cargando = ref(false);
const error = ref(null);
const detalle = ref(null);
const modoEdicion = ref(false);
const guardandoEdicion = ref(false);
const procesandoPago = ref(false);
const itemsEdicion = ref([]);
const observacionesEdicion = ref('');

const esAdmin = computed(() => sesion_store.es_admin);

const observacionesVisibles = computed(() => {
  const obs = detalle.value?.observaciones || '';
  if (esAdmin.value) return obs;
  // Para clientes, limpiar etiquetas internas entre corchetes
  const limpia = obs
    .replace(/\[Comprobante de pago:[^\]]+\]/g, '')
    .replace(/\[Picking:[^\]]+\]/g, '')
    .replace(/\[Cobro Chofer:[^\]]+\]/g, '')
    .replace(/\s*·\s*·?\s*/g, ' · ')
    .trim()
    .replace(/^·\s*/, '')
    .replace(/\s*·$/, '');
  return limpia || null;
});

const comprobanteInfo = computed(() => {
  const obs = detalle.value?.observaciones || '';
  if (!obs.includes('[Comprobante de pago:')) return null;

  const matchUrl = obs.match(/\/uploads\/comprobantes\/[^\s\]]+/);
  const url = matchUrl ? resolver_url_imagen(matchUrl[0]) : null;

  if (obs.includes('[Comprobante de pago: APROBADO')) {
    const matchMonto = obs.match(/\[Comprobante de pago:\s*APROBADO\s*·\s*(\$[\d\.,]+)/);
    const montoTexto = matchMonto ? ` (${matchMonto[1]})` : '';
    return {
      estadoTexto: `Pago Acreditado ✔${esAdmin.value ? montoTexto : ''}`,
      color: 'success',
      esPendiente: false,
      url
    };
  }
  if (obs.includes('[Comprobante de pago: RECHAZADO')) {
    return { estadoTexto: 'Rechazado ✖', color: 'danger', esPendiente: false, url };
  }
  return {
    estadoTexto: 'Comprobante subido · En espera de aprobación ⏳',
    color: 'warning',
    esPendiente: true,
    url
  };
});

const abrirComprobanteAdjunto = () => {
  if (comprobanteInfo.value?.url) {
    vibrar_toque();
    window.open(comprobanteInfo.value.url, '_blank');
  }
};

const abrirEnMaps = (lat, lng) => {
  vibrar_toque();
  geolocalizacion_service.abrir_en_google_maps(lat, lng);
};

const aprobarComprobante = async () => {
  if (!props.pedidoId) return;

  const totalSugerido = totalCalculado.value || detalle.value?.total || 0;
  const input = prompt(
    `Revisión de Comprobante:\nVerificá el extracto bancario e ingresá el monto exacto acreditado en cuenta:`,
    totalSugerido.toString()
  );

  if (input === null) return; // Cancelado por el usuario

  const monto = parseFloat(input.replace(/\./g, '').replace(',', '.'));
  if (isNaN(monto) || monto <= 0) {
    alert('Por favor ingresá un monto válido mayor a 0.');
    return;
  }

  procesandoPago.value = true;
  try {
    vibrar_toque();
    await pedidos_service.aprobar_comprobante(props.pedidoId, monto);
    await vibrar_exito();
    await cargarDetalle();
    emit('pedido_actualizado');
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || err.message || 'Error al aprobar el comprobante.');
  } finally {
    procesandoPago.value = false;
  }
};

const rechazarComprobante = async () => {
  if (!props.pedidoId) return;
  const motivo = prompt('Ingresá el motivo del rechazo:', 'Comprobante no acreditado o ilegible');
  if (motivo === null) return;

  procesandoPago.value = true;
  try {
    vibrar_toque();
    await pedidos_service.rechazar_comprobante(props.pedidoId, motivo);
    await vibrar_exito();
    await cargarDetalle();
    emit('pedido_actualizado');
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || err.message || 'Error al rechazar el comprobante.');
  } finally {
    procesandoPago.value = false;
  }
};

const totalCalculado = computed(() => {
  return itemsEdicion.value.reduce((acc, i) => acc + (i.precio_unitario * i.cantidad), 0);
});

watch(
  () => props.abierto,
  async (abierto) => {
    if (abierto && props.pedidoId) {
      modoEdicion.value = false;
      await cargarDetalle();
    }
  }
);

const cargarDetalle = async () => {
  if (!props.pedidoId) return;
  cargando.value = true;
  error.value = null;

  try {
    const res = await pedidos_service.obtener_por_id(props.pedidoId);
    const p = res.pedido || null;
    detalle.value = p ? con_cambios_sin_enviar(p) : null;
    if (detalle.value) {
      itemsEdicion.value = (detalle.value.detalles || []).map((d) => ({
        producto_id: d.producto_id,
        producto_nombre: d.producto_nombre,
        precio_unitario: d.precio_unitario,
        cantidad: d.cantidad
      }));
      observacionesEdicion.value = detalle.value.observaciones || '';
    }
  } catch (err) {
    error.value = err.mensaje || 'Error al obtener los detalles del pedido.';
  } finally {
    cargando.value = false;
  }
};

const activarEdicion = () => {
  modoEdicion.value = true;
  vibrar_toque();
};

const cancelarEdicion = () => {
  modoEdicion.value = false;
  if (detalle.value) {
    itemsEdicion.value = (detalle.value.detalles || []).map((d) => ({
      producto_id: d.producto_id,
      producto_nombre: d.producto_nombre,
      precio_unitario: d.precio_unitario,
      cantidad: d.cantidad
    }));
    observacionesEdicion.value = detalle.value.observaciones || '';
  }
};

const incrementar = (item) => {
  item.cantidad++;
  vibrar_toque();
};

const decrementar = (item) => {
  if (item.cantidad > 1) {
    item.cantidad--;
    vibrar_toque();
  }
};

const eliminarItem = (item) => {
  if (itemsEdicion.value.length <= 1) {
    alert('El pedido debe conservar al menos un artículo.');
    return;
  }
  itemsEdicion.value = itemsEdicion.value.filter((i) => i.producto_id !== item.producto_id);
  vibrar_toque();
};

const guardarCambiosEdicion = async () => {
  guardandoEdicion.value = true;
  try {
    const payload = {
      items: itemsEdicion.value.map((i) => ({
        producto_id: i.producto_id,
        cantidad: i.cantidad
      })),
      observaciones: observacionesEdicion.value
    };
    await pedidos_service.actualizar_pedido(props.pedidoId, payload);
    await vibrar_exito();
    modoEdicion.value = false;
    await cargarDetalle();
    emit('pedido_actualizado');
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || 'Error al actualizar el pedido.');
  } finally {
    guardandoEdicion.value = false;
  }
};

const descargarPdf = async () => {
  if (!detalle.value) return;
  try {
    const blob = await pedidos_service.descargar_comprobante(detalle.value.id);
    await compartir_archivo({
      nombre_archivo: `${detalle.value.numero}.pdf`,
      blob: blob,
      titulo: `Comprobante ${detalle.value.numero}`,
      texto: `Comprobante oficial de orden ${detalle.value.numero} de MercuryGO.`
    });
  } catch (err) {
    alert('Error al generar el comprobante PDF: ' + err.message);
  }
};

const obtener_color_estado = (estado) => {
  switch (estado) {
    case 'Entregado': return 'success';
    case 'EnPuerta': return 'secondary';
    case 'Despachado': return 'primary';
    case 'EnPreparacion': return 'warning';
    case 'Confirmado': return 'tertiary';
    case 'Cancelado': return 'danger';
    default: return 'medium';
  }
};

const formatear_fecha = (fechaStr) => {
  if (!fechaStr) return '';
  const d = new Date(fechaStr);
  return d.toLocaleDateString('es-AR') + ' · ' + d.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' });
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-modal-loading,
.mercury-modal-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 16px;
  text-align: center;
  color: var(--ion-color-medium, #64748b);
}

.mercury-seccion-card {
  margin: 0 0 14px 0;
  border-radius: 12px;
}

.mercury-cabecera-fila {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.mercury-numero-orden {
  font-size: 1.3rem;
  font-weight: 800;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-badge-estado-grande {
  font-size: 0.85rem;
  padding: 6px 10px;
  border-radius: 8px;
}

.mercury-comprobante-seccion {
  margin: 12px 0;
  padding: 12px;
  background: var(--ion-color-step-50, rgba(0, 0, 0, 0.03));
  border-radius: 8px;
  border-left: 4px solid var(--ion-color-primary, #38bdf8);
}

.mercury-gps-seccion {
  margin-top: 12px;
  padding: 10px;
  background: rgba(var(--ion-color-secondary-rgb), 0.08);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.mercury-gps-info {
  display: flex;
  flex-direction: column;
  font-size: 0.9rem;
}

.mercury-gps-fecha {
  font-size: 0.8rem;
  color: var(--ion-color-medium);
}

.mercury-comprobante-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.mercury-comprobante-acciones {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.mercury-botones-pdf-fila {
  margin-top: 14px;
}

.mercury-item-linea {
  --padding-start: 0;
  --padding-end: 0;
}

.mercury-stepper {
  display: flex;
  align-items: center;
  gap: 2px;
}

.mercury-cant-num {
  font-weight: 700;
  min-width: 24px;
  text-align: center;
}

.mercury-edicion-obs {
  margin-top: 12px;
}

.mercury-botones-edicion-acciones {
  display: flex;
  gap: 10px;
  margin-top: 10px;
}

.mercury-total-fila {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 16px;
  padding-top: 12px;
  border-top: 1px solid var(--ion-color-light, #f1f5f9);
  font-size: 1.05rem;
}

.mercury-total-destacado {
  font-size: 1.3rem;
  color: var(--ion-color-primary, #2563eb);
}
</style>
