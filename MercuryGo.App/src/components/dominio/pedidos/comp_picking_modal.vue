<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Preparación de Pedido (Picking)</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="pedido" class="mercury-picking-header">
        <div class="mercury-picking-info">
          <h2>Orden {{ pedido.numero }}</h2>
          <p class="mercury-picking-cliente">Cliente: <strong>{{ pedido.cliente_razon_social }}</strong></p>
        </div>
        <ion-badge :color="esCompleto ? 'success' : 'warning'" class="mercury-picking-badge-resumen">
          {{ esCompleto ? '✔ COMPLETO' : `⚠ CON FALTANTES (${faltantesCount})` }}
        </ion-badge>
      </div>

      <!-- Barra de acciones rápidas -->
      <div class="mercury-picking-acciones-rapidas">
        <ion-button size="small" fill="outline" color="success" @click="marcarTodosListos">
          <ion-icon slot="start" :icon="checkmarkDoneOutline" />
          Marcar Todo Listo ({{ items.length }})
        </ion-button>
      </div>

      <!-- Estado: Cargando detalles del pedido -->
      <div v-if="cargando" class="mercury-picking-loading">
        <ion-spinner name="crescent" color="primary" />
        <p>Cargando lista de artículos a preparar…</p>
      </div>

      <!-- Error al cargar -->
      <div v-else-if="error" class="mercury-picking-error">
        <ion-icon :icon="alertCircleOutline" color="danger" />
        <p>{{ error }}</p>
        <ion-button size="small" fill="outline" color="primary" @click="cargarDetalles">Reintentar</ion-button>
      </div>

      <!-- Lista de Artículos para Picking -->
      <div v-else class="mercury-picking-lista">
        <ion-card
          v-for="item in items"
          :key="item.producto_id"
          class="mercury-picking-card"
          :class="{ 'item-listo': item.estado_item === 'Listo', 'item-falta': item.estado_item === 'EnFalta' }"
        >
          <ion-card-content class="mercury-card-content-picking">
            <div class="mercury-item-top">
              <div class="mercury-item-titulo">
                <ion-icon :icon="cubeOutline" class="mercury-item-icono" />
                <span class="mercury-item-nombre">{{ item.producto_nombre }}</span>
              </div>
              <ion-badge :color="item.estado_item === 'Listo' ? 'success' : 'danger'">
                {{ item.estado_item === 'Listo' ? 'Listo' : 'En Falta' }}
              </ion-badge>
            </div>

            <div class="mercury-item-cantidades">
              <span class="mercury-cant-pedida">Solicitado: <strong>{{ item.cantidad_pedida }} u.</strong></span>
              <span class="mercury-cant-preparada">
                Preparado:
                <strong :class="item.cantidad_preparada < item.cantidad_pedida ? 'text-alerta' : 'text-ok'">
                  {{ item.cantidad_preparada }} u.
                </strong>
              </span>
            </div>

            <!-- Botonera de estado por ítem -->
            <div class="mercury-item-controles">
              <ion-button
                size="small"
                :fill="item.estado_item === 'Listo' ? 'solid' : 'outline'"
                color="success"
                @click="marcarListo(item)"
              >
                <ion-icon slot="start" :icon="checkmarkCircleOutline" />
                Listo
              </ion-button>

              <ion-button
                size="small"
                :fill="item.estado_item === 'EnFalta' ? 'solid' : 'outline'"
                color="danger"
                @click="marcarFalta(item)"
              >
                <ion-icon slot="start" :icon="alertCircleOutline" />
                En Falta
              </ion-button>

              <!-- Stepper para ajustar unidades preparadas si hay faltante parcial -->
              <div v-if="item.estado_item === 'EnFalta'" class="mercury-stepper">
                <ion-button size="small" fill="clear" color="medium" @click="decrementar(item)">
                  <ion-icon :icon="removeCircleOutline" />
                </ion-button>
                <span class="mercury-stepper-valor">{{ item.cantidad_preparada }}</span>
                <ion-button size="small" fill="clear" color="medium" @click="incrementar(item)">
                  <ion-icon :icon="addCircleOutline" />
                </ion-button>
              </div>
            </div>
          </ion-card-content>
        </ion-card>
      </div>

      <!-- Resumen y Botón de Confirmación -->
      <div v-if="!cargando && items.length > 0" class="mercury-picking-footer">
        <div class="mercury-resumen-caja" :class="esCompleto ? 'resumen-ok' : 'resumen-alerta'">
          <p>
            <strong>Resultado:</strong>
            {{ esCompleto ? 'Todos los productos recolectados.' : `Hay ${faltantesCount} producto(s) con faltante.` }}
          </p>
          <small>
            {{ esCompleto ? 'El pedido pasará a estar listo para despacho.' : 'El pedido quedará registrado con faltantes para aviso al Administrador.' }}
          </small>
        </div>

        <ion-button
          expand="block"
          :color="esCompleto ? 'success' : 'warning'"
          :disabled="guardando"
          @click="confirmarPicking"
          class="mercury-btn-confirmar"
        >
          <ion-spinner v-if="guardando" name="dots" />
          <span v-else>
            {{ esCompleto ? '✔ Confirmar Picking Completo' : '⚠ Registrar Picking con Faltantes' }}
          </span>
        </ion-button>
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
  IonBadge,
  IonCard,
  IonCardContent,
  IonIcon,
  IonSpinner
} from '@ionic/vue';
import {
  checkmarkDoneOutline,
  checkmarkCircleOutline,
  alertCircleOutline,
  cubeOutline,
  removeCircleOutline,
  addCircleOutline
} from 'ionicons/icons';

import { pedidos_service } from '@/services/pedidos_service';
import { pedidos_store } from '@/stores/pedidos_store';
import { vibrar_toque, vibrar_exito, vibrar_advertencia } from '@/services/vibracion_service';

const props = defineProps({
  abierto: {
    type: Boolean,
    default: false
  },
  pedido: {
    type: Object,
    default: null
  }
});

const emit = defineEmits(['cerrar', 'picking_completado']);

const cargando = ref(false);
const guardando = ref(false);
const error = ref(null);
const items = ref([]);

const totalItems = computed(() => items.value.length);
const itemsListosCount = computed(() => items.value.filter((i) => i.estado_item === 'Listo').length);
const faltantesCount = computed(() => items.value.filter((i) => i.estado_item === 'EnFalta' || i.cantidad_preparada < i.cantidad_pedida).length);
const esCompleto = computed(() => totalItems.value > 0 && faltantesCount.value === 0);

watch(
  () => props.abierto,
  async (abierto) => {
    if (abierto && props.pedido) {
      await cargarDetalles();
    }
  }
);

const cargarDetalles = async () => {
  if (!props.pedido) return;
  cargando.value = true;
  error.value = null;

  try {
    const res = await pedidos_service.obtener_por_id(props.pedido.id);
    const detalles = res.pedido?.detalles || [];
    items.value = detalles.map((d) => ({
      producto_id: d.producto_id,
      producto_nombre: d.producto_nombre,
      cantidad_pedida: d.cantidad,
      cantidad_preparada: d.cantidad,
      estado_item: 'Listo'
    }));
  } catch (err) {
    error.value = err.mensaje || 'Error al obtener la lista de artículos del pedido.';
  } finally {
    cargando.value = false;
  }
};

const marcarListo = (item) => {
  item.estado_item = 'Listo';
  item.cantidad_preparada = item.cantidad_pedida;
  vibrar_toque();
};

const marcarFalta = (item) => {
  item.estado_item = 'EnFalta';
  if (item.cantidad_preparada >= item.cantidad_pedida) {
    item.cantidad_preparada = 0;
  }
  vibrar_advertencia();
};

const marcarTodosListos = () => {
  items.value.forEach((i) => {
    i.estado_item = 'Listo';
    i.cantidad_preparada = i.cantidad_pedida;
  });
  vibrar_exito();
};

const decrementar = (item) => {
  if (item.cantidad_preparada > 0) {
    item.cantidad_preparada--;
    vibrar_toque();
  }
};

const incrementar = (item) => {
  if (item.cantidad_preparada < item.cantidad_pedida) {
    item.cantidad_preparada++;
    if (item.cantidad_preparada === item.cantidad_pedida) {
      item.estado_item = 'Listo';
      vibrar_exito();
    } else {
      vibrar_toque();
    }
  }
};

const confirmarPicking = async () => {
  if (!props.pedido || items.value.length === 0) return;
  guardando.value = true;

  try {
    const payload = items.value.map((i) => ({
      producto_id: i.producto_id,
      estado_item: i.estado_item,
      cantidad_preparada: i.cantidad_preparada
    }));

    const res = await pedidos_store.actualizar_picking(props.pedido.id, payload);
    if (esCompleto.value) {
      await vibrar_exito();
    } else {
      await vibrar_advertencia();
    }
    emit('picking_completado', res);
    cerrar();
  } catch (err) {
    error.value = err.mensaje || 'Error al guardar el resultado de picking.';
  } finally {
    guardando.value = false;
  }
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-picking-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 14px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--ion-color-light-shade, #e2e8f0);
}

.mercury-picking-info h2 {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-picking-cliente {
  margin: 4px 0 0;
  font-size: 0.9rem;
  color: var(--ion-color-medium, #64748b);
}

.mercury-picking-badge-resumen {
  font-size: 0.8rem;
  padding: 6px 10px;
  border-radius: 8px;
}

.mercury-picking-acciones-rapidas {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 12px;
}

.mercury-picking-loading,
.mercury-picking-error {
  text-align: center;
  padding: 40px 16px;
  color: var(--ion-color-medium, #64748b);
}

.mercury-picking-card {
  margin: 0 0 12px 0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
  border-left: 4px solid var(--ion-color-medium, #94a3b8);
  transition: border-color 0.2s ease;
}

.mercury-picking-card.item-listo {
  border-left-color: var(--ion-color-success, #16a34a);
}

.mercury-picking-card.item-falta {
  border-left-color: var(--ion-color-danger, #dc2626);
  background: rgba(220, 38, 38, 0.02);
}

.mercury-card-content-picking {
  padding: 12px;
}

.mercury-item-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.mercury-item-titulo {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-item-icono {
  font-size: 1.1rem;
  color: var(--ion-color-primary, #0284c7);
}

.mercury-item-cantidades {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  margin-bottom: 10px;
  color: var(--ion-color-medium, #64748b);
}

.text-ok {
  color: var(--ion-color-success, #16a34a);
}

.text-alerta {
  color: var(--ion-color-danger, #dc2626);
}

.mercury-item-controles {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mercury-stepper {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-left: auto;
  background: var(--ion-color-light, #f1f5f9);
  border-radius: 8px;
  padding: 2px 6px;
}

.mercury-stepper-valor {
  font-weight: 700;
  font-size: 0.95rem;
  min-width: 24px;
  text-align: center;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-picking-footer {
  margin-top: 18px;
  padding-top: 14px;
  border-top: 1px solid var(--ion-color-light-shade, #e2e8f0);
}

.mercury-resumen-caja {
  padding: 12px;
  border-radius: 10px;
  margin-bottom: 14px;
}

.mercury-resumen-caja.resumen-ok {
  background: rgba(22, 163, 74, 0.08);
  border: 1px solid rgba(22, 163, 74, 0.3);
  color: #15803d;
}

.mercury-resumen-caja.resumen-alerta {
  background: rgba(234, 179, 8, 0.1);
  border: 1px solid rgba(234, 179, 8, 0.35);
  color: #a16207;
}

.mercury-resumen-caja p {
  margin: 0 0 4px;
  font-size: 0.9rem;
}

.mercury-resumen-caja small {
  font-size: 0.8rem;
  opacity: 0.9;
}

.mercury-btn-confirmar {
  margin-top: 8px;
  font-weight: 600;
}
</style>
