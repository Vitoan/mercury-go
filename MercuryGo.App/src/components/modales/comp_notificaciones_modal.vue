<template>
  <ion-modal :is-open="abierto" @didDismiss="emit('cerrar')">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>🔔 Centro de Notificaciones</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="emit('cerrar')">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div class="mercury-notif-acciones-top" v-if="notificaciones.length > 0">
        <ion-button fill="clear" size="small" @click="marcarTodas">
          Marcar todas como leídas
        </ion-button>
        <ion-button fill="clear" size="small" color="medium" @click="cargarBandeja">
          Actualizar
        </ion-button>
      </div>

      <div v-if="cargando" class="mercury-notif-centrado">
        <ion-spinner name="crescent" />
        <p>Cargando avisos...</p>
      </div>

      <div v-else-if="notificaciones.length === 0" class="mercury-notif-vacio">
        <ion-icon :icon="notificationsOffOutline" class="icono-vacio" />
        <h3>Sin notificaciones</h3>
        <p>No tienes mensajes ni alertas pendientes en tu bandeja.</p>
      </div>

      <ion-list v-else lines="full">
        <ion-item
          v-for="item in notificaciones"
          :key="item.id"
          :class="{ 'item-no-leido': !item.leida }"
          button
          @click="tocarNotificacion(item)"
        >
          <ion-icon
            slot="start"
            :icon="obtenerIcono(item.tipo)"
            :color="item.leida ? 'medium' : 'primary'"
          />
          <ion-label class="ion-text-wrap">
            <div class="notif-header">
              <h2>{{ item.titulo }}</h2>
              <ion-badge v-if="!item.leida" color="danger" class="badge-nuevo">NUEVO</ion-badge>
            </div>
            <p>{{ item.mensaje }}</p>
            <ion-note class="notif-fecha">{{ formatearFecha(item.fecha) }}</ion-note>
          </ion-label>
        </ion-item>
      </ion-list>
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
  IonList,
  IonItem,
  IonLabel,
  IonIcon,
  IonBadge,
  IonNote,
  IonSpinner
} from '@ionic/vue';
import {
  notificationsOffOutline,
  navigateOutline,
  cubeOutline,
  cardOutline,
  informationCircleOutline
} from 'ionicons/icons';
import { notificaciones_service } from '@/services/notificaciones_service';

const props = defineProps({
  abierto: { type: Boolean, default: false }
});

const emit = defineEmits(['cerrar', 'actualizado']);

const notificaciones = ref([]);
const cargando = ref(false);

const cargarBandeja = async () => {
  try {
    cargando.value = true;
    const res = await notificaciones_service.obtener_bandeja(30);
    notificaciones.value = res?.items || [];
    emit('actualizado', res?.no_leidas || 0);
  } catch (err) {
    console.warn('Error al cargar notificaciones:', err);
  } finally {
    cargando.value = false;
  }
};

const tocarNotificacion = async (item) => {
  if (!item.leida) {
    item.leida = true;
    try {
      await notificaciones_service.marcar_leida(item.id);
      const restantes = notificaciones.value.filter(n => !n.leida).length;
      emit('actualizado', restantes);
    } catch (err) {
      console.warn('Error al marcar leída:', err);
    }
  }
};

const marcarTodas = async () => {
  try {
    await notificaciones_service.marcar_todas_leidas();
    notificaciones.value.forEach(n => (n.leida = true));
    emit('actualizado', 0);
  } catch (err) {
    console.warn('Error al marcar todas leídas:', err);
  }
};

const obtenerIcono = (tipo) => {
  switch (tipo) {
    case 'GPS':
      return navigateOutline;
    case 'PEDIDO':
      return cubeOutline;
    case 'PAGO':
      return cardOutline;
    default:
      return informationCircleOutline;
  }
};

const formatearFecha = (fechaStr) => {
  if (!fechaStr) return '';
  try {
    const d = new Date(fechaStr);
    return d.toLocaleString('es-AR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return fechaStr;
  }
};

watch(() => props.abierto, (val) => {
  if (val) {
    cargarBandeja();
  }
});
</script>

<style scoped>
.mercury-notif-acciones-top {
  display: flex;
  justify-content: space-between;
  margin-bottom: 8px;
}
.mercury-notif-centrado,
.mercury-notif-vacio {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 40px 20px;
  color: var(--ion-color-medium);
}
.icono-vacio {
  font-size: 56px;
  margin-bottom: 12px;
}
.item-no-leido {
  --background: rgba(var(--ion-color-primary-rgb), 0.06);
  font-weight: 600;
}
.notif-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.notif-header h2 {
  font-size: 15px;
  margin: 0;
}
.badge-nuevo {
  font-size: 9px;
  padding: 2px 5px;
}
.notif-fecha {
  font-size: 11px;
  color: var(--ion-color-medium);
}
</style>
