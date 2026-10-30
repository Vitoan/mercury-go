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
              <p class="mercury-precio-unitario">{{ formatearMoneda(item.precio) }} c/u</p>
              <p class="mercury-subtotal-item">Subtotal: <strong>{{ formatearMoneda(item.precio * item.cantidad) }}</strong></p>
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

        <!-- Selector de Cliente para Administrador -->
        <ion-item v-if="sesion_store.es_admin" lines="inset" style="margin-top: 14px;">
          <ion-label position="stacked">Cliente del Pedido (Admin) *</ion-label>
          <ion-select
            v-model="clienteSeleccionadoId"
            placeholder="Seleccionar cliente destino..."
            interface="action-sheet"
            cancel-text="Cancelar"
          >
            <ion-select-option
              v-for="c in listaClientes"
              :key="c.id"
              :value="c.id"
            >
              {{ c.razon_social || c.nombre }} (CUIT: {{ c.cuit || 'S/D' }})
            </ion-select-option>
          </ion-select>
        </ion-item>

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
              <span class="mercury-monto-total">${{ totalEstimado }}</span>
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
import { formatearMoneda } from '@/utils/formato';
import { ref, computed, watch, onMounted } from 'vue';
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
  IonSpinner,
  IonSelect,
  IonSelectOption
} from '@ionic/vue';
import { cartOutline, cubeOutline, trashOutline } from 'ionicons/icons';
import { carrito_store } from '@/stores/carrito_store';
import { sesion_store } from '@/stores/sesion_store';
import { clientes_service } from '@/services/clientes_service';

const props = defineProps({
  abierto: { type: Boolean, default: false }
});

const emit = defineEmits(['cerrar', 'pedido_creado']);

const totalEstimado = computed(() => {
  const monto = Number(carrito_store.subtotal_total) || 0;
  return monto.toLocaleString('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
});

const observaciones = ref(carrito_store.observaciones);
const clienteSeleccionadoId = ref(null);
const listaClientes = ref([]);
const cargandoClientes = ref(false);

const cargarClientes = async () => {
  if (!sesion_store.es_admin) return;
  cargandoClientes.value = true;
  try {
    const res = await clientes_service.obtener_clientes({ tamano: 100 });
    listaClientes.value = res.clientes || res.items || (Array.isArray(res) ? res : []);
    if (!clienteSeleccionadoId.value && listaClientes.value.length > 0) {
      clienteSeleccionadoId.value = listaClientes.value[0].id;
    }
  } catch (err) {
    console.warn('Error al cargar clientes en carrito:', err);
  } finally {
    cargandoClientes.value = false;
  }
};

watch(() => props.abierto, (val) => {
  if (val && sesion_store.es_admin) {
    cargarClientes();
  }
});

onMounted(() => {
  if (sesion_store.es_admin) {
    cargarClientes();
  }
});

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
  if (sesion_store.es_admin && !clienteSeleccionadoId.value) {
    alert('Por favor seleccioná el cliente destino para este pedido.');
    return;
  }
  carrito_store.observaciones = observaciones.value;
  try {
    const res = await carrito_store.confirmar_pedido(sesion_store.es_admin ? clienteSeleccionadoId.value : null);
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
