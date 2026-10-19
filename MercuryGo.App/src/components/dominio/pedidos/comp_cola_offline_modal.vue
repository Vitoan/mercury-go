<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Cola de Envíos Offline</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div class="mercury-cola-header">
        <div class="mercury-cola-resumen">
          <span class="mercury-cola-badge" :class="offline_store.en_linea ? 'online' : 'offline'">
            {{ offline_store.en_linea ? '🟢 Conectado' : '🔴 Sin Conexión' }}
          </span>
          <span class="mercury-cola-pendientes">
            {{ offline_store.pendientes_count }} pendiente(s) de envío
          </span>
        </div>

        <div class="mercury-cola-acciones-top">
          <ion-button
            size="small"
            color="primary"
            :disabled="offline_store.sincronizando || !offline_store.en_linea || offline_store.cola.length === 0"
            @click="sincronizarAhora"
          >
            <ion-spinner v-if="offline_store.sincronizando" name="dots" />
            <span v-else>Sincronizar Ahora</span>
          </ion-button>

          <ion-button
            size="small"
            fill="outline"
            color="medium"
            @click="offline_store.limpiar_enviadas"
          >
            Limpiar Enviadas
          </ion-button>
        </div>
      </div>

      <div v-if="offline_store.cola.length === 0" class="mercury-cola-vacia">
        <ion-icon :icon="checkmarkCircleOutline" color="success" class="mercury-icono-vacio" />
        <h3>Cola vacía</h3>
        <p>No tenés operaciones pendientes de sincronización con el servidor.</p>
      </div>

      <div v-else class="mercury-cola-lista">
        <ion-card
          v-for="accion in offline_store.cola"
          :key="accion.id"
          class="mercury-accion-card"
          :class="`estado-${accion.estado}`"
        >
          <ion-card-content>
            <div class="mercury-accion-fila-top">
              <span class="mercury-accion-tipo">{{ formatearTipo(accion.tipo) }}</span>
              <ion-badge :color="obtenerColorEstado(accion.estado)">
                {{ accion.estado.toUpperCase() }}
              </ion-badge>
            </div>

            <p class="mercury-accion-pedido">
              Orden ID: <strong>#{{ accion.pedido_id }}</strong>
            </p>
            <p class="mercury-accion-fecha">
              Registrado: {{ formatearFecha(accion.ocurrido_en) }} · Intentos: {{ accion.intentos }}
            </p>

            <div v-if="accion.error" class="mercury-accion-error">
              <ion-icon :icon="alertCircleOutline" color="danger" />
              <span>{{ accion.error }}</span>
            </div>

            <div v-if="accion.estado === 'error' || accion.estado === 'conflicto'" class="mercury-accion-botones">
              <ion-button size="small" fill="outline" color="primary" @click="reintentar(accion.id)">
                Reintentar
              </ion-button>
            </div>
          </ion-card-content>
        </ion-card>
      </div>
    </ion-content>
  </ion-modal>
</template>

<script setup>
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
  IonIcon,
  IonSpinner
} from '@ionic/vue';
import { checkmarkCircleOutline, alertCircleOutline } from 'ionicons/icons';
import { use_offline_store } from '@/stores/offline_store';

const props = defineProps({
  abierto: { type: Boolean, default: false }
});

const emit = defineEmits(['cerrar']);

const offline_store = use_offline_store();

const cerrar = () => {
  emit('cerrar');
};

const sincronizarAhora = () => {
  offline_store.sincronizar();
};

const reintentar = (id) => {
  offline_store.reintentar_accion(id);
};

const formatearTipo = (tipo) => {
  switch (tipo) {
    case 'ENTREGAR': return '✔ Confirmación de Entrega';
    case 'EN_PUERTA': return '📍 Chofer En Puerta';
    case 'NO_ENTREGADO': return '⚠ Intento Fallido';
    case 'COBRO': return '💵 Cobro en Destino';
    case 'ENTREGA_PARCIAL': return '📦 Entrega Parcial (Rechazo)';
    case 'PICKING_ITEM': return '📋 Picking de Depósito';
    case 'DESPACHADO': return '🚚 Despacho a Ruta';
    default: return tipo;
  }
};

const obtenerColorEstado = (estado) => {
  switch (estado) {
    case 'enviada': return 'success';
    case 'enviando': return 'primary';
    case 'conflicto': return 'danger';
    case 'error': return 'warning';
    default: return 'medium';
  }
};

const formatearFecha = (fechaStr) => {
  if (!fechaStr) return '';
  const d = new Date(fechaStr);
  return d.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
};
</script>

<style scoped>
.mercury-cola-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 10px;
}
.mercury-cola-resumen {
  display: flex;
  align-items: center;
  gap: 10px;
}
.mercury-cola-badge.online {
  color: var(--ion-color-success);
  font-weight: 700;
  font-size: 0.9rem;
}
.mercury-cola-badge.offline {
  color: var(--ion-color-danger);
  font-weight: 700;
  font-size: 0.9rem;
}
.mercury-cola-pendientes {
  font-size: 0.85rem;
  color: var(--ion-color-medium);
}
.mercury-cola-acciones-top {
  display: flex;
  gap: 8px;
}
.mercury-cola-vacia {
  text-align: center;
  padding: 40px 16px;
  color: var(--ion-color-medium);
}
.mercury-icono-vacio {
  font-size: 3rem;
  margin-bottom: 8px;
}
.mercury-accion-card {
  margin: 0 0 10px 0;
  border-radius: 10px;
}
.mercury-accion-fila-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.mercury-accion-tipo {
  font-weight: 700;
  font-size: 0.95rem;
}
.mercury-accion-pedido {
  margin: 4px 0;
  font-size: 0.9rem;
}
.mercury-accion-fecha {
  margin: 0;
  font-size: 0.8rem;
  color: var(--ion-color-medium);
}
.mercury-accion-error {
  margin-top: 8px;
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  color: var(--ion-color-danger);
}
.mercury-accion-botones {
  margin-top: 10px;
}
</style>
