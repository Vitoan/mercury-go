<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="danger">
        <ion-title>Registrar Entrega Fallida</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="pedido" class="mercury-fallida-intro">
        <h2>Orden {{ pedido.numero }}</h2>
        <p class="mercury-fallida-cliente">Cliente: <strong>{{ pedido.cliente_razon_social }}</strong></p>
        <p class="mercury-fallida-sub">Seleccioná el motivo por el cual no se pudo concretar la entrega en domicilio:</p>
      </div>

      <ion-card class="mercury-fallida-card">
        <ion-card-content>
          <ion-item lines="inset">
            <ion-label position="stacked">Motivo de no entrega</ion-label>
            <ion-select v-model="motivo" interface="action-sheet" placeholder="Elegir motivo...">
              <ion-select-option value="ClienteAusente">👤 Cliente Ausente / No atiende</ion-select-option>
              <ion-select-option value="DireccionIncorrecta">📍 Dirección Incorrecta o Inexistente</ion-select-option>
              <ion-select-option value="LocalCerrado">🔒 Comercio Cerrado fuera de horario</ion-select-option>
              <ion-select-option value="RechazoTotal">🚫 Rechazo Total por el Cliente</ion-select-option>
              <ion-select-option value="FuerzaMayor">⚠️ Fuerza Mayor / Clima / Zona inaccesible</ion-select-option>
              <ion-select-option value="Otro">📝 Otro motivo particular</ion-select-option>
            </ion-select>
          </ion-item>

          <ion-item lines="inset" style="margin-top: 12px;">
            <ion-label position="stacked">Nota u observación aclaratoria</ion-label>
            <ion-textarea
              v-model="nota"
              placeholder="Detalle adicional (ej: se tocó timbre reiteradas veces, guardia sin autorización)..."
              :rows="3"
            />
          </ion-item>
        </ion-card-content>
      </ion-card>

      <div style="margin-top: 24px;">
        <ion-button
          expand="block"
          color="danger"
          :disabled="!motivo || enviando"
          @click="confirmarNoEntrega"
        >
          <ion-spinner v-if="enviando" name="dots" />
          <span v-else>⚠ Registrar No Entregado</span>
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
  IonCardContent,
  IonItem,
  IonLabel,
  IonSelect,
  IonSelectOption,
  IonTextarea,
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

const emit = defineEmits(['cerrar', 'entrega_fallida_registrada']);

const motivo = ref('ClienteAusente');
const nota = ref('');
const enviando = ref(false);

watch(
  () => props.pedido,
  () => {
    motivo.value = 'ClienteAusente';
    nota.value = '';
  }
);

const confirmarNoEntrega = async () => {
  if (!props.pedido || !motivo.value) return;
  enviando.value = true;
  // Hora del intento fallido en el dispositivo: vale también para la cola offline.
  const ocurridoEn = new Date().toISOString();
  try {
    const offline = use_offline_store();
    if (offline.sin_conexion) {
      offline.encolar_accion('NO_ENTREGADO', props.pedido.id, {
        motivo: motivo.value,
        nota: nota.value,
        ocurrido_en: ocurridoEn
      });
      await vibrar_exito();
      emit('entrega_fallida_registrada', {
        mensaje: 'Entrega fallida registrada sin conexión. Se enviará al reconectar.'
      });
      cerrar();
      return;
    }
    const res = await pedidos_service.registrar_no_entregado(
      props.pedido.id,
      motivo.value,
      nota.value,
      null,
      nueva_clave_idempotencia(),
      ocurridoEn
    );
    await vibrar_exito();
    emit('entrega_fallida_registrada', res);
    cerrar();
  } catch (err) {
    await vibrar_error();
    alert(err.mensaje || 'Error al registrar intento fallido.');
  } finally {
    enviando.value = false;
  }
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-fallida-intro h2 {
  font-size: 1.2rem;
  font-weight: 800;
  margin: 0;
}
.mercury-fallida-cliente {
  margin: 4px 0;
  color: var(--ion-color-medium);
  font-size: 0.9rem;
}
.mercury-fallida-sub {
  margin: 8px 0 16px 0;
  font-size: 0.88rem;
}
.mercury-fallida-card {
  margin: 0;
  border-radius: 12px;
}
</style>
