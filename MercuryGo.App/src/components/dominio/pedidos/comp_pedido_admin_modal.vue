<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Cargar Pedido Telefónico / WhatsApp</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div class="mercury-modal-intro">
        <p>Carga de orden de despacho asistida por Administrador. Los precios oficiales se validan automáticamente en el servidor.</p>
      </div>

      <!-- 1. Selección de Cliente Comercial -->
      <ion-card class="mercury-seccion-card">
        <ion-card-header>
          <ion-card-subtitle>Paso 1: Destinatario</ion-card-subtitle>
          <ion-card-title>Seleccionar Cliente</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none" class="mercury-item-select">
            <ion-icon :icon="businessOutline" slot="start" color="primary" />
            <ion-select
              v-model="clienteSeleccionadoId"
              placeholder="Elegir comercio cliente…"
              interface="action-sheet"
              cancel-text="Cancelar"
            >
              <ion-select-option
                v-for="c in clientes"
                :key="c.id"
                :value="c.id"
              >
                {{ c.razon_social }} (CUIT: {{ c.cuit }})
              </ion-select-option>
            </ion-select>
          </ion-item>
        </ion-card-content>
      </ion-card>

      <!-- 2. Selección de Productos del Catálogo -->
      <ion-card class="mercury-seccion-card">
        <ion-card-header>
          <ion-card-subtitle>Paso 2: Mercadería</ion-card-subtitle>
          <ion-card-title>Agregar Productos</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <!-- Selector rápido de producto para agregar -->
          <div class="mercury-agregar-fila">
            <ion-select
              v-model="productoIdParaAgregar"
              placeholder="Buscar producto a incluir…"
              interface="alert"
              cancel-text="Cancelar"
              class="mercury-select-prod"
            >
              <ion-select-option
                v-for="p in productosDisponibles"
                :key="p.id"
                :value="p.id"
              >
                {{ p.nombre }} — ${{ p.precio?.toLocaleString('es-AR') }}
              </ion-select-option>
            </ion-select>
            <ion-button
              size="small"
              color="primary"
              :disabled="!productoIdParaAgregar"
              @click="agregarProductoSeleccionado"
            >
              <ion-icon slot="icon-only" :icon="addOutline" />
            </ion-button>
          </div>

          <!-- Tabla de Artículos Agregados al Pedido -->
          <div v-if="itemsAgregados.length === 0" class="mercury-sin-items">
            <ion-icon :icon="cartOutline" />
            <p>Todavía no agregaste ningún producto a este pedido.</p>
          </div>

          <div v-else class="mercury-items-tabla">
            <div
              v-for="item in itemsAgregados"
              :key="item.producto_id"
              class="mercury-item-agregado"
            >
              <div class="mercury-item-desc">
                <strong>{{ item.nombre }}</strong>
                <span>${{ item.precio?.toLocaleString('es-AR') }} c/u</span>
              </div>

              <!-- Stepper -->
              <div class="mercury-stepper">
                <ion-button size="small" fill="clear" color="medium" @click="decrementarCantidad(item)">
                  <ion-icon :icon="removeCircleOutline" />
                </ion-button>
                <span class="mercury-cant-valor">{{ item.cantidad }}</span>
                <ion-button size="small" fill="clear" color="medium" @click="incrementarCantidad(item)">
                  <ion-icon :icon="addCircleOutline" />
                </ion-button>
              </div>

              <div class="mercury-item-subtotal">
                <span>${{ (item.cantidad * item.precio)?.toLocaleString('es-AR') }}</span>
                <ion-button size="small" fill="clear" color="danger" @click="quitarItem(item)">
                  <ion-icon :icon="trashOutline" />
                </ion-button>
              </div>
            </div>
          </div>
        </ion-card-content>
      </ion-card>

      <!-- 3. Observaciones y Estado Inicial -->
      <ion-card class="mercury-seccion-card">
        <ion-card-header>
          <ion-card-subtitle>Paso 3: Detalles y Despacho</ion-card-subtitle>
          <ion-card-title>Notas y Estado Inicial</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none" class="mercury-input-item">
            <ion-textarea
              v-model="observaciones"
              label="Observaciones de entrega:"
              label-placement="stacked"
              placeholder="Ej: Pedido tomado por llamada telefónica / WhatsApp..."
              :rows="3"
            />
          </ion-item>

          <div class="mercury-estado-inicial">
            <label class="mercury-label-estado">Estado inicial del pedido:</label>
            <ion-segment v-model="estadoInicial" color="primary">
              <ion-segment-button value="Confirmado">
                <ion-label>Confirmado (Directo)</ion-label>
              </ion-segment-button>
              <ion-segment-button value="Pendiente">
                <ion-label>Pendiente de Pago</ion-label>
              </ion-segment-button>
            </ion-segment>
          </div>
        </ion-card-content>
      </ion-card>

      <!-- Mensaje de Error si ocurre -->
      <div v-if="errorMensaje" class="mercury-error-aviso">
        <ion-icon :icon="alertCircleOutline" />
        <span>{{ errorMensaje }}</span>
      </div>

      <!-- Resumen y Botón de Creación -->
      <div class="mercury-modal-footer">
        <div class="mercury-total-resumen">
          <span>Total Estimado:</span>
          <strong>${{ totalEstimado.toLocaleString('es-AR') }}</strong>
        </div>

        <ion-button
          expand="block"
          color="success"
          :disabled="!esValido || guardando"
          @click="crearPedido"
        >
          <ion-spinner v-if="guardando" name="dots" />
          <span v-else>Confirmar y Crear Pedido</span>
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
  IonCard,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonCardContent,
  IonItem,
  IonLabel,
  IonSelect,
  IonSelectOption,
  IonTextarea,
  IonSegment,
  IonSegmentButton,
  IonIcon,
  IonSpinner
} from '@ionic/vue';
import {
  businessOutline,
  cartOutline,
  addOutline,
  addCircleOutline,
  removeCircleOutline,
  trashOutline,
  alertCircleOutline
} from 'ionicons/icons';

import { clientes_service } from '@/services/clientes_service';
import { productos_service } from '@/services/productos_service';
import { pedidos_store } from '@/stores/pedidos_store';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';

const props = defineProps({
  abierto: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['cerrar', 'pedido_creado']);

const clientes = ref([]);
const productos = ref([]);
const clienteSeleccionadoId = ref(null);
const productoIdParaAgregar = ref(null);
const itemsAgregados = ref([]);
const observaciones = ref('Pedido tomado por teléfono / WhatsApp.');
const estadoInicial = ref('Confirmado');

const cargando = ref(false);
const guardando = ref(false);
const errorMensaje = ref(null);

const productosDisponibles = computed(() => {
  const idsAgregados = new Set(itemsAgregados.value.map((i) => i.producto_id));
  return productos.value.filter((p) => !idsAgregados.has(p.id) && p.disponible !== false);
});

const totalEstimado = computed(() => {
  return itemsAgregados.value.reduce((acc, i) => acc + i.cantidad * i.precio, 0);
});

const esValido = computed(() => {
  return clienteSeleccionadoId.value && itemsAgregados.value.length > 0;
});

watch(
  () => props.abierto,
  async (abierto) => {
    if (abierto) {
      await cargarCatalogos();
    }
  }
);

const cargarCatalogos = async () => {
  cargando.value = true;
  errorMensaje.value = null;
  try {
    const [resClientes, resProductos] = await Promise.all([
      clientes_service.obtener_clientes({ tamano: 100 }),
      productos_service.obtener_resumen()
    ]);
    clientes.value = resClientes.clientes || [];
    productos.value = resProductos.productos || [];
  } catch (err) {
    errorMensaje.value = err.mensaje || 'Error al cargar los clientes y productos.';
  } finally {
    cargando.value = false;
  }
};

const agregarProductoSeleccionado = () => {
  if (!productoIdParaAgregar.value) return;
  const prod = productos.value.find((p) => p.id === productoIdParaAgregar.value);
  if (prod) {
    itemsAgregados.value.push({
      producto_id: prod.id,
      nombre: prod.nombre,
      precio: prod.precio,
      cantidad: 1
    });
    productoIdParaAgregar.value = null;
    vibrar_toque();
  }
};

const incrementarCantidad = (item) => {
  item.cantidad++;
  vibrar_toque();
};

const decrementarCantidad = (item) => {
  if (item.cantidad > 1) {
    item.cantidad--;
    vibrar_toque();
  } else {
    quitarItem(item);
  }
};

const quitarItem = (item) => {
  itemsAgregados.value = itemsAgregados.value.filter((i) => i.producto_id !== item.producto_id);
  vibrar_toque();
};

const crearPedido = async () => {
  if (!esValido.value) return;
  guardando.value = true;
  errorMensaje.value = null;

  try {
    const payload = {
      cliente_id: clienteSeleccionadoId.value,
      observaciones: observaciones.value,
      estado: estadoInicial.value,
      items: itemsAgregados.value.map((i) => ({
        producto_id: i.producto_id,
        cantidad: i.cantidad
      }))
    };

    const res = await pedidos_store.crear_orden(payload);
    await vibrar_exito();
    emit('pedido_creado', res);
    limpiarFormulario();
    cerrar();
  } catch (err) {
    await vibrar_error();
    errorMensaje.value = err.mensaje || 'No se pudo crear el pedido en el servidor.';
  } finally {
    guardando.value = false;
  }
};

const limpiarFormulario = () => {
  clienteSeleccionadoId.value = null;
  productoIdParaAgregar.value = null;
  itemsAgregados.value = [];
  observaciones.value = 'Pedido tomado por teléfono / WhatsApp.';
  estadoInicial.value = 'Confirmado';
};

const cerrar = () => {
  emit('cerrar');
};
</script>

<style scoped>
.mercury-modal-intro {
  margin-bottom: 12px;
  font-size: 0.88rem;
  color: var(--ion-color-medium, #64748b);
  line-height: 1.4;
}

.mercury-seccion-card {
  margin: 0 0 14px 0;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
}

.mercury-item-select {
  --background: var(--ion-color-light, #f8fafc);
  border-radius: 8px;
  margin-top: 4px;
}

.mercury-agregar-fila {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}

.mercury-select-prod {
  flex: 1;
  background: var(--ion-color-light, #f8fafc);
  border-radius: 8px;
  padding: 8px 12px;
}

.mercury-sin-items {
  text-align: center;
  padding: 24px 12px;
  color: var(--ion-color-medium, #94a3b8);
}

.mercury-sin-items ion-icon {
  font-size: 2.2rem;
  margin-bottom: 4px;
}

.mercury-items-tabla {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.mercury-item-agregado {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 10px;
  background: var(--ion-color-light, #f8fafc);
  border-radius: 8px;
}

.mercury-item-desc {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.mercury-item-desc strong {
  font-size: 0.9rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mercury-item-desc span {
  font-size: 0.8rem;
  color: var(--ion-color-medium, #64748b);
}

.mercury-stepper {
  display: flex;
  align-items: center;
  gap: 2px;
  background: #ffffff;
  border-radius: 6px;
  padding: 2px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  margin: 0 8px;
}

.mercury-cant-valor {
  font-weight: 700;
  font-size: 0.95rem;
  min-width: 24px;
  text-align: center;
}

.mercury-item-subtotal {
  display: flex;
  align-items: center;
  gap: 4px;
  font-weight: 700;
  font-size: 0.95rem;
  color: var(--ion-color-primary, #0284c7);
}

.mercury-input-item {
  --background: var(--ion-color-light, #f8fafc);
  border-radius: 8px;
  margin-bottom: 12px;
}

.mercury-estado-inicial {
  margin-top: 8px;
}

.mercury-label-estado {
  display: block;
  font-size: 0.85rem;
  font-weight: 600;
  margin-bottom: 6px;
  color: var(--ion-color-medium, #64748b);
}

.mercury-error-aviso {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(220, 38, 38, 0.1);
  color: #dc2626;
  padding: 10px 14px;
  border-radius: 8px;
  margin: 12px 0;
  font-size: 0.88rem;
}

.mercury-modal-footer {
  margin-top: 20px;
  padding-top: 14px;
  border-top: 1px solid var(--ion-color-light-shade, #e2e8f0);
}

.mercury-total-resumen {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
  font-size: 1.15rem;
}

.mercury-total-resumen strong {
  font-size: 1.35rem;
  color: var(--ion-color-primary, #0284c7);
}
</style>
