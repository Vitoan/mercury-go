<template>
  <ion-modal :is-open="abierto" @didDismiss="$emit('cerrar')">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>{{ cliente ? 'Editar Cliente Comercial' : 'Nuevo Cliente B2B' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="$emit('cerrar')">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div class="mercury-modal-stack">
        <div class="mercury-modal-subtitulo">
          <p>Complete los datos fiscales y de entrega para la emisión de remitos y ruteo LIFO.</p>
        </div>

        <ion-list class="mercury-form-list">
          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.razon_social"
              label="Razón Social / Comercio *"
              label-placement="stacked"
              placeholder="Ej: Distribuidora Los Andes S.R.L."
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.cuit"
              label="CUIT Comercial *"
              label-placement="stacked"
              placeholder="30-XXXXXXXX-X"
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.telefono"
              label="Teléfono de Contacto"
              label-placement="stacked"
              placeholder="266-4123456"
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.email"
              type="email"
              label="Email de Facturación"
              label-placement="stacked"
              placeholder="compras@comercio.com.ar"
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.direccion"
              label="Dirección de Descarga / Depósito"
              label-placement="stacked"
              placeholder="Av. San Martín 1234"
            />
          </ion-item>

          <ion-item lines="none" class="mercury-form-item">
            <ion-input
              v-model="formulario.localidad"
              label="Localidad / Zona Logística"
              label-placement="stacked"
              placeholder="San Luis / Villa Mercedes / Merlo"
            />
          </ion-item>
        </ion-list>

        <!-- El error viene estrictamente de la validación del servidor (HTTP 400).
             El modal no se cierra para que el usuario no pierda lo tipeado. -->
        <div v-if="error" class="mercury-alerta-error">
          <ion-icon :icon="alertCircleOutline" class="icono-alerta" />
          <span>{{ error }}</span>
        </div>

        <div class="mercury-acciones-modal">
          <ion-button
            expand="block"
            color="primary"
            class="mercury-boton-guardar"
            :disabled="guardando"
            @click="enviar"
          >
            <ion-spinner v-if="guardando" name="crescent" />
            <span v-else>{{ cliente ? 'Guardar Cambios' : 'Registrar Cliente' }}</span>
          </ion-button>
        </div>
      </div>
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { reactive, watch } from 'vue';
import {
  IonModal,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonButtons,
  IonButton,
  IonContent,
  IonList,
  IonItem,
  IonInput,
  IonSpinner,
  IonIcon
} from '@ionic/vue';
import { alertCircleOutline } from 'ionicons/icons';

const props = defineProps({
  abierto: {
    type: Boolean,
    default: false
  },
  cliente: {
    type: Object,
    default: null
  },
  error: {
    type: String,
    default: ''
  },
  guardando: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['cerrar', 'guardar']);

const FORMULARIO_VACIO = {
  razon_social: '',
  cuit: '',
  telefono: '',
  email: '',
  direccion: '',
  localidad: ''
};

const formulario = reactive({ ...FORMULARIO_VACIO });

// Se inicializa cada vez que se abre el modal para no compartir estado entre clientes:
watch(
  () => props.abierto,
  (estaAbierto) => {
    if (!estaAbierto) return;
    if (props.cliente) {
      formulario.razon_social = props.cliente.razon_social || '';
      formulario.cuit = props.cliente.cuit || '';
      formulario.telefono = props.cliente.telefono || '';
      formulario.email = props.cliente.email || '';
      formulario.direccion = props.cliente.direccion || '';
      formulario.localidad = props.cliente.localidad || '';
    } else {
      Object.assign(formulario, FORMULARIO_VACIO);
    }
  }
);

const enviar = () => {
  emit('guardar', { ...formulario });
};
</script>

<style scoped>
.mercury-modal-stack {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.mercury-modal-subtitulo p {
  margin: 0;
  font-size: 0.88rem;
  color: var(--ion-color-step-600, #94a3b8);
}

.mercury-form-list {
  background: var(--ion-color-step-50, rgba(255, 255, 255, 0.03));
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-form-item {
  --background: transparent;
}

.mercury-alerta-error {
  display: flex;
  align-items: center;
  gap: 10px;
  background: rgba(239, 68, 68, 0.12);
  color: var(--ion-color-danger, #ef4444);
  padding: 10px 14px;
  border-radius: 8px;
  border-left: 4px solid #ef4444;
  font-size: 0.88rem;
}

.icono-alerta {
  font-size: 1.2rem;
  flex-shrink: 0;
}

.mercury-acciones-modal {
  margin-top: 8px;
}

.mercury-boton-guardar {
  --border-radius: 8px;
  font-weight: 600;
  text-transform: none;
}
</style>
