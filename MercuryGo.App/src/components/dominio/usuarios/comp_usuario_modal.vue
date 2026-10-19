<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Alta de Usuario</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content class="ion-padding">
      <ion-card class="mercury-card-modal">
        <ion-card-content>
          <ion-item lines="inset">
            <ion-input
              v-model="formulario.nombre"
              label="Nombre y Apellido *"
              label-placement="stacked"
              placeholder="Ej: Marcelo Gómez"
            />
          </ion-item>

          <ion-item lines="inset">
            <ion-input
              v-model="formulario.email"
              type="email"
              label="Email Corporativo *"
              label-placement="stacked"
              placeholder="mgomez@mercurygo.local"
            />
          </ion-item>

          <ion-item lines="inset">
            <ion-input
              v-model="formulario.telefono"
              type="tel"
              label="Teléfono"
              label-placement="stacked"
              placeholder="266-4112233"
            />
          </ion-item>

          <ion-item lines="inset">
            <ion-input
              v-model="formulario.password"
              type="password"
              label="Contraseña Temporal *"
              label-placement="stacked"
              placeholder="Mínimo 8 caracteres"
            />
          </ion-item>

          <ion-item lines="none">
            <ion-select
              v-model="formulario.rol_id"
              label="Rol de Acceso Logístico"
              label-placement="stacked"
              interface="popover"
              placeholder="Seleccionar rol..."
            >
              <ion-select-option :value="null">Sin rol (queda en espera de habilitación)</ion-select-option>
              <ion-select-option v-for="r in roles" :key="r.id" :value="r.id">
                {{ r.nombre }} ({{ r.codigo }})
              </ion-select-option>
            </ion-select>
          </ion-item>

          <div v-if="errorModal" class="mercury-modal-error">
            <span>{{ errorModal }}</span>
          </div>

          <ion-button
            expand="block"
            style="margin-top: 1rem;"
            :disabled="guardando"
            @click="guardar"
          >
            <ion-spinner v-if="guardando" name="crescent" />
            <span v-else>Guardar Usuario</span>
          </ion-button>
        </ion-card-content>
      </ion-card>
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { ref, watch, computed } from 'vue';
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
  IonInput,
  IonSelect,
  IonSelectOption,
  IonSpinner
} from '@ionic/vue';
import { useUsuariosStore } from '@/stores/usuarios_store';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';

const props = defineProps({
  abierto: { type: Boolean, default: false }
});

const emit = defineEmits(['cerrar', 'usuario_creado']);

const usuariosStore = useUsuariosStore();
const roles = computed(() => usuariosStore.roles);
const guardando = ref(false);
const errorModal = ref('');

const formulario = ref({
  nombre: '',
  email: '',
  telefono: '',
  password: '',
  rol_id: null
});

watch(
  () => props.abierto,
  (abierto) => {
    if (abierto) {
      errorModal.value = '';
      formulario.value = {
        nombre: '',
        email: '',
        telefono: '',
        password: '',
        rol_id: null
      };
    }
  }
);

const cerrar = () => {
  emit('cerrar');
};

const guardar = async () => {
  errorModal.value = '';
  if (!formulario.value.nombre?.trim()) {
    errorModal.value = 'El nombre es obligatorio.';
    vibrar_error();
    return;
  }
  if (!formulario.value.email?.includes('@')) {
    errorModal.value = 'Email inválido.';
    vibrar_error();
    return;
  }
  if (!formulario.value.password || formulario.value.password.length < 8) {
    errorModal.value = 'La contraseña requiere al menos 8 caracteres.';
    vibrar_error();
    return;
  }

  guardando.value = true;
  try {
    vibrar_toque();
    const exito = await usuariosStore.crear(formulario.value);
    if (exito) {
      await vibrar_exito();
      emit('usuario_creado', formulario.value);
      cerrar();
    } else {
      await vibrar_error();
      errorModal.value = usuariosStore.error || 'Error al guardar usuario.';
    }
  } catch (err) {
    await vibrar_error();
    errorModal.value = err.message || 'Error al guardar usuario.';
  } finally {
    guardando.value = false;
  }
};
</script>

<style scoped>
.mercury-card-modal {
  margin: 0;
  border-radius: 12px;
}
.mercury-modal-error {
  color: var(--ion-color-danger, #ef4444);
  font-size: 0.85rem;
  margin-top: 8px;
  text-align: center;
}
</style>
