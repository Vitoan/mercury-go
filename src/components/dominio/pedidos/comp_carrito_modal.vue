<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Carrito de Compras B2B</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="!carrito_store.hay_items" class="mercury-carrito-vacio">
        <ion-icon :icon="cartOutline" class="mercury-icono-vacio" />
        <h3>Tu carrito está vacío</h3>
        <p>Seleccioná productos del catálogo para armar tu orden de compra.</p>
        <ion-button fill="outline" style="margin-top: 16px;" @click="cerrar">
          Ver Catálogo
        </ion-button>
      </div>

      <div v-else class="mercury-carrito-contenido">
        <!-- Lista de productos en el carrito -->
        <ion-list>
          <ion-item v-for="item in carrito_store.items" :key="item.producto_id" class="mercury-carrito-item">
            <ion-icon :icon="cubeOutline" slot="start" color="primary" />
            <ion-label>
              <h3>{{ item.nombre }}</h3>
              <p class="mercury-precio-unitario">${{ item.precio.toLocaleString('es-AR') }} c/u</p>
              <p class="mercury-subtotal-item">Subtotal: <strong>${{ (item.precio * item.cantidad).toLocaleString('es-AR') }}</strong></p>
            </ion-label>
            <div slot="end" class="mercury-control-cantidad">
              <ion-button size="small" fill="outline" color="medium" @click="decrementar(item)">-</ion-button>
              <span class="mercury-cantidad-valor">{{ item.cantidad }}</span>
              <ion-button size="small" fill="outline" color="primary" @click="incrementar(item)">+</ion-button>
              <ion-button size="small" fill="clear" color="danger" @click="eliminar(item)">
                <ion-icon :icon="trashOutline" />
              </ion-button>
            </div>
          </ion-item>
        </ion-list>

        <!-- Observaciones -->
        <ion-item lines="inset" style="margin-top: 14px;">
          <ion-label position="stacked">Observaciones / Datos de Entrega</ion-label>
          <ion-textarea
            v-model="observaciones"
            placeholder="Horario preferido, instrucciones para el chofer..."
            :rows="2"
          />
        </ion-item>

        <!-- Resumen de Totales -->
        <ion-card class="mercury-card-resumen">
          <ion-card-content>
            <div class="mercury-fila-resumen">
              <span>Bultos / Unidades:</span>
              <strong>{{ carrito_store.cantidad_total }}</strong>
            </div>
            <div class="mercury-fila-resumen mercury-total-destacado">
              <span>Total Estimado:</span>
              <span class="mercury-monto-total">${{ carrito_store.subtotal_total.toLocaleString('es-AR') }}</span>
            </div>
          </ion-card-content>
        </ion-card>

        <!-- Botones de Acción -->
        <div class="mercury-acciones-carrito">
          <ion-button
            expand="block"
            color="success"
            :disabled="carrito_store.enviando"
            @click="enviarPedido"
          >
            <ion-spinner v-if="carrito_store.enviando" name="dots" />
            <span v-else>Confirmar Orden (▶ Pendiente)</span>
          </ion-button>

          <ion-button
            expand="block"
            fill="clear"
            color="medium"
            :disabled="carrito_store.enviando"
            @click="vaciar"
          >
            Vaciar Carrito
          </ion-button>
        </div>
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
  IonList,
  IonItem,
  IonLabel,
  IonIcon,
  IonTextarea,
  IonCard,
  IonCardContent,
  IonSpinner
} from '@ionic/vue';
import { cartOutline, cubeOutline, trashOutline } from 'ionicons/icons';
import { carrito_store } from '@/stores/carrito_store';

const props = defineProps({
  abierto: { type: Boolean, default: false }
});

const emit = defineEmits(['cerrar', 'pedido_creado']);

const observaciones = ref(carrito_store.observaciones);

watch(() => carrito_store.observaciones, (val) => {
  observaciones.value = val;
});

const cerrar = () => {
  emit('cerrar');
};

const incrementar = (item) => {
  carrito_store.cambiar_cantidad(item.producto_id, item.cantidad + 1);
};

const decrementar = (item) => {
  carrito_store.cambiar_cantidad(item.producto_id, item.cantidad - 1);
};

const eliminar = (item) => {
  carrito_store.quitar_producto(item.producto_id);
};

const vaciar = () => {
  carrito_store.vaciar_carrito();
};

const enviarPedido = async () => {
  carrito_store.observaciones = observaciones.value;
  try {
    const res = await carrito_store.confirmar_pedido();
    emit('pedido_creado', res);
    cerrar();
  } catch (err) {
    alert(err.message || 'No se pudo crear el pedido.');
  }
};
</script>

<style scoped>
.mercury-carrito-vacio {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  min-height: 60vh;
  color: var(--ion-color-step-600, #94a3b8);
}

.mercury-icono-vacio {
  font-size: 4rem;
  color: var(--ion-color-step-400, #cbd5e1);
  margin-bottom: 12px;
}

.mercury-carrito-item {
  --padding-start: 4px;
  --padding-end: 4px;
}

.mercury-precio-unitario {
  font-size: 0.82rem;
  color: var(--ion-color-step-600, #64748b);
}

.mercury-subtotal-item {
  font-size: 0.88rem;
  color: var(--ion-color-primary, #38bdf8);
  margin-top: 2px;
}

.mercury-control-cantidad {
  display: flex;
  align-items: center;
  gap: 4px;
}

.mercury-cantidad-valor {
  min-width: 24px;
  text-align: center;
  font-weight: 700;
  font-size: 0.95rem;
}

.mercury-card-resumen {
  margin: 16px 0 0 0;
  border-radius: 12px;
  background: var(--ion-color-step-50, #f8fafc);
}

.mercury-fila-resumen {
  display: flex;
  justify-content: space-between;
  margin-bottom: 8px;
  font-size: 0.9rem;
}

.mercury-total-destacado {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px dashed var(--ion-color-step-200, #e2e8f0);
  font-size: 1.1rem;
  font-weight: 700;
}

.mercury-monto-total {
  color: var(--ion-color-success, #22c55e);
}

.mercury-acciones-carrito {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
