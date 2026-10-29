<template>
  <ion-page>
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-menu-button></ion-menu-button>
        </ion-buttons>
        <ion-title>{{ titulo }}</ion-title>
        <ion-buttons slot="end">
          <!-- Badge Sin Conexión -->
          <ion-chip
            v-if="offline_store.sin_conexion"
            color="warning"
            class="chip-sin-conexion"
            title="Modo fuera de línea activo"
          >
            <ion-icon :icon="cloudOfflineOutline" />
            <ion-label>Sin red</ion-label>
          </ion-chip>

          <!-- Botón / Badge de Cola Offline (pendientes o conflictos) -->
          <ion-button
            v-if="offline_store.pendientes_count > 0 || offline_store.conflictos_count > 0"
            :title="offline_store.conflictos_count > 0
              ? 'Hay envíos rechazados por el servidor'
              : 'Envíos pendientes de sincronización'"
            :color="offline_store.conflictos_count > 0 ? 'danger' : 'warning'"
            @click="mostrarModalCola = true"
            class="btn-cola-offline"
          >
            <ion-icon slot="icon-only" :icon="syncOutline" :class="{ 'rotando': offline_store.sincronizando }" />
            <ion-badge :color="offline_store.conflictos_count > 0 ? 'danger' : 'warning'" class="badge-cola">
              {{ offline_store.conflictos_count > 0 ? offline_store.conflictos_count : offline_store.pendientes_count }}
            </ion-badge>
          </ion-button>

          <!-- Campana de Notificaciones -->
          <ion-button
            v-if="sesion_store.autenticado"
            title="Notificaciones"
            @click="mostrarModalNotif = true"
            class="btn-notificaciones"
          >
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

    <!-- Modal de Cola Offline -->
    <comp-cola-offline-modal
      :abierto="mostrarModalCola"
      @cerrar="mostrarModalCola = false"
    />
  </ion-page>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue';
import { 
  IonPage, IonHeader, IonToolbar, IonTitle, IonButtons, IonButton,
  IonMenuButton, IonContent, IonRefresher, IonRefresherContent,
  IonIcon, IonBadge, IonChip, IonLabel
} from '@ionic/vue';
import { notificationsOutline, cloudOfflineOutline, syncOutline } from 'ionicons/icons';
import CompNotificacionesModal from '@/components/modales/comp_notificaciones_modal.vue';
import CompColaOfflineModal from '@/components/dominio/pedidos/comp_cola_offline_modal.vue';
import { notificaciones_service } from '@/services/notificaciones_service';
import { sesion_store } from '@/stores/sesion_store';
import { use_offline_store } from '@/stores/offline_store';

const offline_store = use_offline_store();
const mostrarModalCola = ref(false);

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
  if (!sesion_store.autenticado) return;
  try {
    const res = await notificaciones_service.obtener_bandeja(1);
    if (res && typeof res.no_leidas === 'number') {
      noLeidas.value = res.no_leidas;
    }
  } catch {
    // Si no está autenticado o falla, silenciar
  }
};

let intervaloNotificaciones = null;

onMounted(() => {
  consultarNoLeidas();
  // Sondeo suave cada 35 segundos para mantener actualizado el badge de la campana
  if (!intervaloNotificaciones) {
    intervaloNotificaciones = setInterval(() => {
      if (sesion_store.autenticado && typeof document !== 'undefined' && !document.hidden) {
        consultarNoLeidas();
      }
    }, 35000);
  }
});

onUnmounted(() => {
  if (intervaloNotificaciones) {
    clearInterval(intervaloNotificaciones);
    intervaloNotificaciones = null;
  }
});

watch(() => sesion_store.autenticado, (activo) => {
  if (activo) {
    consultarNoLeidas();
  } else {
    noLeidas.value = 0;
  }
});

const ejecutar_actualizar = (event) => {
  consultarNoLeidas();
  emit('actualizar', event);
};
</script>

<style scoped>
.btn-notificaciones,
.btn-cola-offline {
  position: relative;
}
.badge-cola {
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 10px;
  padding: 2px 4px;
  border-radius: 10px;
}
.chip-sin-conexion {
  height: 26px;
  font-size: 11px;
  font-weight: 600;
  margin-right: 4px;
}
.rotando {
  animation: girar 1s linear infinite;
}
@keyframes girar {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
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
