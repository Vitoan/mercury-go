<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Cobro en Destino · Chofer</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="pedido" class="mercury-cobro-intro">
        <h2>Orden {{ pedido.numero }}</h2>
        <p class="mercury-cobro-cliente">Cliente: <strong>{{ pedido.cliente_razon_social }}</strong></p>
        <p class="mercury-cobro-total">Importe de la orden: <strong>${{ pedido.total?.toLocaleString('es-AR') }}</strong></p>
      </div>

      <ion-card class="mercury-cobro-card">
        <ion-card-header>
          <ion-card-subtitle>Registro de Cobranza</ion-card-subtitle>
          <ion-card-title>Detalles del Pago</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="inset">
            <ion-label position="stacked">Monto Cobrado ($)</ion-label>
            <ion-input
              v-model.number="monto"
              type="number"
              placeholder="0.00"
              required
            />
          </ion-item>

          <ion-item lines="inset" style="margin-top: 10px;">
            <ion-label position="stacked">Medio de Pago</ion-label>
            <ion-select v-model="metodo" interface="action-sheet">
              <ion-select-option value="Efectivo">💵 Efectivo</ion-select-option>
              <ion-select-option value="Transferencia">📱 Transferencia Directa / QR</ion-select-option>
              <ion-select-option value="Cheque">📑 Cheque al Día</ion-select-option>
            </ion-select>
          </ion-item>
        </ion-card-content>
      </ion-card>

      <div style="margin-top: 24px;">
        <ion-button
          expand="block"
          color="success"
          :disabled="!monto || monto <= 0 || enviando"
          @click="confirmarCobro"
        >
          <ion-spinner v-if="enviando" name="dots" />
          <span v-else>✔ Confirmar y Registrar Cobro</span>
        </ion-button>
      </div>
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { ref, watch } from 'vue';
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
  IonItem,
  IonLabel,
  IonInput,
  IonSelect,
  IonSelectOption,
  IonSpinner
} from '@ionic/vue';
import { pedidos_service } from '@/services/pedidos_service';
import { use_offline_store } from '@/stores/offline_store';
import { vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { nueva_clave_idempotencia } from '@/utils/idempotencia';

const props = defineProps({
  abierto: { type: Boolean, default: false },
  pedido: { type: Object, default: null }
});

const emit = defineEmits(['cerrar', 'cobro_registrado']);

const monto = ref(0);
const metodo = ref('Efectivo');
const enviando = ref(false);

watch(
  () => props.pedido,
  (p) => {
    if (p) {
      monto.value = p.total || 0;
      metodo.value = 'Efectivo';
    }
  },
  { immediate: true }
);

const confirmarCobro = async () => {
  if (!props.pedido || monto.value <= 0 || enviando.value) return;
  enviando.value = true;
  // La hora del evento se captura en el dispositivo: es la que vale si el cobro
  // se registra sin conexión y se sincroniza más tarde.
  const ocurridoEn = new Date().toISOString();
  try {
    const offline = use_offline_store();
    if (offline.sin_conexion) {
      offline.encolar_accion('COBRO', props.pedido.id, {
        monto: Number(monto.value),
        metodo: metodo.value,
        ocurrido_en: ocurridoEn
      });
      await vibrar_exito();
      emit('cobro_registrado', {
        mensaje: 'Cobro de $' + Number(monto.value).toLocaleString('es-AR') + ' guardado sin conexión. Se enviará al reconectar.'
      });
      cerrar();
      return;
    }
    const res = await pedidos_service.registrar_cobro_chofer(
      props.pedido.id,
      monto.value,
      metodo.value,
      nueva_clave_idempotencia(),
      ocurridoEn
    );
    await vibrar_exito();
    emit('cobro_registrado', res);
    cerrar();
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || 'Error al registrar el cobro.');
  } finally {
    enviando.value = false;
  }
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-cobro-intro {
  margin-bottom: 16px;
}

.mercury-cobro-intro h2 {
  font-size: 1.25rem;
  font-weight: 800;
  margin: 0;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-cobro-cliente {
  margin: 4px 0;
  color: var(--ion-color-medium, #64748b);
  font-size: 0.9rem;
}

.mercury-cobro-total {
  font-size: 1.05rem;
  margin: 6px 0 0 0;
}

.mercury-cobro-card {
  margin: 0;
  border-radius: 12px;
}
</style>
