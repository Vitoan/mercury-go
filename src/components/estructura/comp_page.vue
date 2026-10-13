<template>
  <ion-page>
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-menu-button></ion-menu-button>
        </ion-buttons>
        <ion-title>{{ titulo }}</ion-title>
        <ion-buttons slot="end">
          <!-- Campana de Notificaciones (Semana 7) -->
          <ion-button title="Notificaciones" @click="mostrarModalNotif = true" class="btn-notificaciones">
            <ion-icon slot="icon-only" :icon="notificationsOutline" />
            <ion-badge v-if="noLeidas > 0" color="danger" class="badge-notif">
              {{ noLeidas > 99 ? '99+' : noLeidas }}
            </ion-badge>
          </ion-button>
          <slot name="acciones" />
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content :fullscreen="true" class="ion-padding">
      <ion-refresher v-if="mostrar_actualizar" slot="fixed" @ionRefresh="ejecutar_actualizar">
        <ion-refresher-content></ion-refresher-content>
      </ion-refresher>
      <slot />
    </ion-content>

    <!-- Modal In-App de Notificaciones -->
    <comp-notificaciones-modal
      :abierto="mostrarModalNotif"
      @cerrar="mostrarModalNotif = false"
      @actualizado="actualizarConteo"
    />
  </ion-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { 
  IonPage, IonHeader, IonToolbar, IonTitle, IonButtons, IonButton,
  IonMenuButton, IonContent, IonRefresher, IonRefresherContent,
  IonIcon, IonBadge
} from '@ionic/vue';
import { notificationsOutline } from 'ionicons/icons';
import CompNotificacionesModal from '@/components/modales/comp_notificaciones_modal.vue';
import { notificaciones_service } from '@/services/notificaciones_service';

defineProps({
  titulo: { type: String, required: true },
  mostrar_actualizar: { type: Boolean, default: false }
});

const emit = defineEmits(['actualizar']);

const mostrarModalNotif = ref(false);
const noLeidas = ref(0);

const actualizarConteo = (total) => {
  noLeidas.value = total;
};

const consultarNoLeidas = async () => {
  try {
    const res = await notificaciones_service.obtener_bandeja(1);
    if (res && typeof res.no_leidas === 'number') {
      noLeidas.value = res.no_leidas;
    }
  } catch {
    // Si no está autenticado o falla, silenciar
  }
};

onMounted(() => {
  consultarNoLeidas();
});

const ejecutar_actualizar = (event) => {
  consultarNoLeidas();
  emit('actualizar', event);
};
</script>

<style scoped>
.btn-notificaciones {
  position: relative;
}
.badge-notif {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 10px;
  padding: 2px 4px;
  border-radius: 10px;
}
</style>
