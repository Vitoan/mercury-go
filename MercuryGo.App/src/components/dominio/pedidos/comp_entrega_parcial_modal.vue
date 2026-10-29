<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="warning">
        <ion-title>Entrega Parcial / Rechazo</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="pedido" class="mercury-parcial-intro">
        <h2>Orden {{ pedido.numero }}</h2>
        <p class="mercury-parcial-cliente">Cliente: <strong>{{ pedido.cliente_razon_social }}</strong></p>
        <p class="mercury-parcial-sub">Marcá los ítems que el cliente rechaza o no acepta recibir:</p>
      </div>

      <div v-if="cargando" class="mercury-parcial-loading">
        <ion-spinner name="crescent" color="primary" />
        <p>Cargando líneas del pedido…</p>
      </div>

      <div v-else class="mercury-parcial-lista">
        <ion-card v-for="linea in lineas" :key="linea.id" class="mercury-parcial-card">
          <ion-card-content>
            <div class="mercury-linea-header">
              <span class="mercury-prod-nombre">{{ linea.producto_nombre }}</span>
              <ion-badge color="medium">Total: {{ linea.cantidad_pedida }} u.</ion-badge>
            </div>

            <div class="mercury-cant-rechazo-fila">
              <span>Unidades Rechazadas:</span>
              <div class="mercury-stepper-rechazo">
                <ion-button
                  size="small"
                  fill="outline"
                  color="medium"
                  :disabled="linea.cantidad_rechazada <= 0"
                  @click="linea.cantidad_rechazada--"
                >
                  -
                </ion-button>
                <strong class="mercury-valor-rechazo">{{ linea.cantidad_rechazada }}</strong>
                <ion-button
                  size="small"
                  fill="outline"
                  color="warning"
                  :disabled="linea.cantidad_rechazada >= linea.cantidad_pedida"
                  @click="linea.cantidad_rechazada++"
                >
                  +
                </ion-button>
              </div>
            </div>

            <p class="mercury-cant-entregada-resumen">
              Se entregarán: <strong>{{ linea.cantidad_pedida - linea.cantidad_rechazada }} u.</strong>
            </p>
          </ion-card-content>
        </ion-card>

        <ion-item lines="inset" style="margin-top: 14px;">
          <ion-label position="stacked">Motivo general del rechazo parcial</ion-label>
          <ion-input
            v-model="motivo"
            placeholder="Ej: Mercadería dañada en trayecto, cliente no solicitó ese ítem..."
          />
        </ion-item>

        <div style="margin-top: 20px;">
          <ion-button
            expand="block"
            color="warning"
            :disabled="enviando || totalRechazado === 0"
            @click="confirmarEntregaParcial"
          >
            <ion-spinner v-if="enviando" name="dots" />
            <span v-else>Confirmar Entrega Parcial ({{ totalRechazado }} rech.)</span>
          </ion-button>
        </div>
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
  IonCardContent,
  IonBadge,
  IonItem,
  IonLabel,
  IonInput,
  IonSpinner
} from '@ionic/vue';
import { pedidos_service } from '@/services/pedidos_service';
import { use_offline_store } from '@/stores/offline_store';
import { vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { geolocalizacion_service } from '@/services/geolocalizacion_service';
import { nueva_clave_idempotencia } from '@/utils/idempotencia';

const props = defineProps({
  abierto: { type: Boolean, default: false },
  pedido: { type: Object, default: null }
});

const emit = defineEmits(['cerrar', 'entrega_parcial_registrada']);

const lineas = ref([]);
const motivo = ref('');
const cargando = ref(false);
const enviando = ref(false);

const totalRechazado = computed(() => {
  return lineas.value.reduce((acc, l) => acc + (l.cantidad_rechazada || 0), 0);
});

watch(
  () => props.abierto,
  async (ab) => {
    if (ab && props.pedido) {
      cargando.value = true;
      try {
        const res = await pedidos_service.obtener_por_id(props.pedido.id);
        const p = res?.pedido;
        lineas.value = (p?.detalles || []).map((d) => ({
          id: d.id,
          producto_id: d.producto_id,
          producto_nombre: d.producto_nombre,
          cantidad_pedida: d.cantidad || d.cantidad_pedida,
          cantidad_rechazada: 0
        }));
        motivo.value = '';
      } catch (err) {
        alert('Error al cargar líneas: ' + (err.mensaje || err.message));
      } finally {
        cargando.value = false;
      }
    }
  }
);

const confirmarEntregaParcial = async () => {
  if (!props.pedido || totalRechazado.value === 0) return;
  enviando.value = true;
  // Hora del evento antes del GPS; vale igual para el envío directo y la cola.
  const ocurridoEn = new Date().toISOString();
  try {
    let lat = null;
    let lng = null;
    try {
      const pos = await geolocalizacion_service.obtener_posicion_actual();
      lat = pos.latitud;
      lng = pos.longitud;
    } catch {}

    const payloadLineas = lineas.value.map((l) => ({
      detalle_id: l.id,
      cantidad_entregada: l.cantidad_pedida - l.cantidad_rechazada,
      cantidad_rechazada: l.cantidad_rechazada,
      motivo_rechazo: l.cantidad_rechazada > 0 ? motivo.value || 'Rechazado por cliente' : null
    }));

    const offline = use_offline_store();
    if (offline.sin_conexion) {
      offline.encolar_accion('ENTREGA_PARCIAL', props.pedido.id, {
        lineas: payloadLineas,
        motivo: motivo.value,
        ocurrido_en: ocurridoEn
      }, lat, lng);
      await vibrar_exito();
      emit('entrega_parcial_registrada', {
        mensaje: 'Entrega parcial encolada sin conexión. Se enviará al reconectar.'
      });
      cerrar();
      return;
    }

    const res = await pedidos_service.registrar_entrega_parcial(
      props.pedido.id,
      payloadLineas,
      motivo.value,
      lat,
      lng,
      null,
      null,
      nueva_clave_idempotencia(),
      ocurridoEn
    );

    await vibrar_exito();
    emit('entrega_parcial_registrada', res);
    cerrar();
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || 'Error al registrar entrega parcial.');
  } finally {
    enviando.value = false;
  }
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-parcial-intro h2 {
  font-size: 1.2rem;
  font-weight: 800;
  margin: 0;
}
.mercury-parcial-cliente {
  margin: 4px 0;
  color: var(--ion-color-medium);
  font-size: 0.9rem;
}
.mercury-parcial-sub {
  margin: 6px 0 14px 0;
  font-size: 0.88rem;
}
.mercury-parcial-card {
  margin: 0 0 10px 0;
  border-radius: 10px;
}
.mercury-linea-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.mercury-prod-nombre {
  font-weight: 700;
  font-size: 0.95rem;
}
.mercury-cant-rechazo-fila {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.mercury-stepper-rechazo {
  display: flex;
  align-items: center;
  gap: 12px;
}
.mercury-valor-rechazo {
  font-size: 1.1rem;
  color: var(--ion-color-warning-shade);
}
.mercury-cant-entregada-resumen {
  margin: 8px 0 0 0;
  font-size: 0.85rem;
  color: var(--ion-color-medium);
}
.mercury-parcial-loading {
  text-align: center;
  padding: 40px;
}
</style>
