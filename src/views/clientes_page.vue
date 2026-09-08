<template>
  <comp-page titulo="Clientes Comerciales" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Buscador con debounce en servidor -->
    <comp-buscador
      v-model="busqueda"
      placeholder="Buscar por razón social, CUIT o teléfono…"
      @buscar="alBuscar"
    />

    <!-- Estado 1: Cargando primera página -->
    <comp-esqueleto v-if="clientes_store.cargando && !clientes_store.hay_clientes" />

    <!-- Estado 2: Error al conectar -->
    <comp-estado-error
      v-else-if="clientes_store.error && !clientes_store.hay_clientes"
      :mensaje="clientes_store.error"
      @reintentar="recargar"
    />

    <!-- Estado 3: Sin resultados -->
    <comp-estado-vacio
      v-else-if="!clientes_store.hay_clientes"
      mensaje="No se encontraron clientes o cuentas comerciales."
    />

    <!-- Estado 4: Lista paginada con acciones ABM -->
    <comp-lista
      v-else
      :cargando="clientes_store.cargando"
      :hay_mas="clientes_store.hay_mas"
      :mostrados="clientes_store.mostrados"
      :total="clientes_store.total"
      @cargar_mas="clientes_store.cargar_mas"
    >
      <ion-item-sliding v-for="c in clientes_store.clientes" :key="c.id">
        <ion-item class="mercury-cliente-card">
          <ion-icon :icon="businessOutline" slot="start" class="mercury-icono-cliente" />
          <ion-label>
            <div class="mercury-cliente-cabecera">
              <h3>{{ c.razon_social }}</h3>
              <ion-badge color="primary" class="mercury-badge-cuit">CUIT {{ c.cuit }}</ion-badge>
            </div>
            <p v-if="c.localidad || c.direccion" class="mercury-cliente-direccion">
              📍 {{ c.direccion ? c.direccion + ' · ' : '' }}{{ c.localidad || 'Zona Central' }}
            </p>
            <div class="mercury-cliente-contacto">
              <span v-if="c.telefono">📞 {{ c.telefono }}</span>
              <span v-if="c.email">✉️ {{ c.email }}</span>
            </div>
          </ion-label>

          <!-- Botones de acción directa -->
          <ion-buttons slot="end" class="mercury-botones-accion">
            <ion-button fill="clear" color="primary" @click.stop="abrirEditar(c)">
              <ion-icon slot="icon-only" :icon="createOutline" />
            </ion-button>
            <ion-button fill="clear" color="danger" @click.stop="confirmarBaja(c)">
              <ion-icon slot="icon-only" :icon="trashOutline" />
            </ion-button>
          </ion-buttons>
        </ion-item>

        <!-- Opciones al deslizar (Mobile gesture) -->
        <ion-item-options side="end">
          <ion-item-option color="primary" @click="abrirEditar(c)">
            <ion-icon slot="top" :icon="createOutline" />
            Editar
          </ion-item-option>
          <ion-item-option color="danger" @click="confirmarBaja(c)">
            <ion-icon slot="top" :icon="trashOutline" />
            Baja
          </ion-item-option>
        </ion-item-options>
      </ion-item-sliding>
    </comp-lista>

    <!-- Botón flotante para registrar un nuevo cliente B2B -->
    <ion-fab slot="fixed" vertical="bottom" horizontal="end">
      <ion-fab-button color="primary" class="mercury-fab" @click="abrirNuevo">
        <ion-icon :icon="addOutline" />
      </ion-fab-button>
    </ion-fab>

    <!-- Modal de Formulario ABM -->
    <comp-cliente-modal
      :abierto="modalAbierto"
      :cliente="clienteSeleccionado"
      :guardando="guardando"
      :error="errorModal"
      @cerrar="cerrarModal"
      @guardar="guardarCliente"
    />

    <!-- Alerta de Confirmación de Baja Lógica -->
    <ion-alert
      :is-open="mostrarAlertaBaja"
      header="Confirmar Baja Lógica"
      :message="`¿Desea dar de baja al cliente '${clienteAEliminar?.razon_social}'? No se borrarán sus remitos históricos.`"
      :buttons="[
        { text: 'Cancelar', role: 'cancel', handler: () => { mostrarAlertaBaja = false; } },
        { text: 'Dar de Baja', role: 'destructive', handler: ejecutarBaja }
      ]"
      @didDismiss="mostrarAlertaBaja = false"
    />
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import {
  IonItem,
  IonItemSliding,
  IonItemOptions,
  IonItemOption,
  IonLabel,
  IonIcon,
  IonBadge,
  IonButtons,
  IonButton,
  IonFab,
  IonFabButton,
  IonAlert
} from '@ionic/vue';
import {
  businessOutline,
  createOutline,
  trashOutline,
  addOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import CompClienteModal from '../components/dominio/clientes/comp_cliente_modal.vue';

import { clientes_store } from '@/stores/clientes_store';

const busqueda = ref('');
const modalAbierto = ref(false);
const clienteSeleccionado = ref(null);
const guardando = ref(false);
const errorModal = ref('');

const mostrarAlertaBaja = ref(false);
const clienteAEliminar = ref(null);

const alBuscar = (texto) => {
  clientes_store.establecer_busqueda(texto);
};

const recargar = (event = null) => {
  clientes_store.cargar(true, event);
};

const abrirNuevo = () => {
  clienteSeleccionado.value = null;
  errorModal.value = '';
  modalAbierto.value = true;
};

const abrirEditar = (cliente) => {
  clienteSeleccionado.value = cliente;
  errorModal.value = '';
  modalAbierto.value = true;
};

const cerrarModal = () => {
  if (guardando.value) return;
  modalAbierto.value = false;
  clienteSeleccionado.value = null;
  errorModal.value = '';
};

const guardarCliente = async (datos) => {
  guardando.value = true;
  errorModal.value = '';

  try {
    const id = clienteSeleccionado.value ? clienteSeleccionado.value.id : null;
    await clientes_store.guardar_cliente(datos, id);
    // Si la API aceptó, cerramos el modal
    modalAbierto.value = false;
    clienteSeleccionado.value = null;
  } catch (err) {
    // Si la API rechazó (400 Bad Request), el modal PERMANECE ABIERTO y muestra el error
    errorModal.value = err.message || 'Error al procesar los datos en el servidor.';
  } finally {
    guardando.value = false;
  }
};

const confirmarBaja = (cliente) => {
  clienteAEliminar.value = cliente;
  mostrarAlertaBaja.value = true;
};

const ejecutarBaja = async () => {
  if (!clienteAEliminar.value) return;
  try {
    await clientes_store.eliminar_cliente(clienteAEliminar.value.id);
  } catch (err) {
    alert(err.message || 'No se pudo dar de baja al cliente.');
  } finally {
    clienteAEliminar.value = null;
    mostrarAlertaBaja.value = false;
  }
};

onMounted(() => {
  clientes_store.cargar(true);
});
</script>

<style scoped>
.mercury-cliente-card {
  --padding-start: 12px;
  --padding-end: 8px;
  --padding-top: 10px;
  --padding-bottom: 10px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-icono-cliente {
  font-size: 1.6rem;
  color: var(--ion-color-primary, #38bdf8);
}

.mercury-cliente-cabecera {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.mercury-cliente-cabecera h3 {
  font-weight: 600;
  font-size: 1rem;
  margin: 0;
  color: var(--ion-text-color, #f8fafc);
}

.mercury-badge-cuit {
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.3px;
  padding: 3px 6px;
  border-radius: 4px;
}

.mercury-cliente-direccion {
  margin: 4px 0 2px 0;
  font-size: 0.85rem;
  color: var(--ion-color-step-600, #94a3b8);
}

.mercury-cliente-contacto {
  display: flex;
  gap: 12px;
  font-size: 0.8rem;
  color: var(--ion-color-step-500, #64748b);
  margin-top: 2px;
}

.mercury-botones-accion ion-button {
  --padding-start: 4px;
  --padding-end: 4px;
  font-size: 1.1rem;
}

.mercury-fab {
  --box-shadow: 0 4px 14px rgba(2, 132, 199, 0.4);
  font-size: 1.4rem;
}
</style>