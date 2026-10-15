<template>
  <comp-page titulo="Clientes Comerciales" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Segmentos para alternar entre Gestión de Clientes y Cuentas Corrientes/Deuda -->
    <div class="mercury-segmento-contenedor">
      <ion-segment v-model="vistaActiva" @ionChange="alCambiarVista">
        <ion-segment-button value="clientes">
          <ion-icon :icon="businessOutline" />
          <ion-label>Clientes</ion-label>
        </ion-segment-button>
        <ion-segment-button value="finanzas">
          <ion-icon :icon="walletOutline" />
          <ion-label>
            Cuentas Corrientes
            <ion-badge v-if="metricasFinanzas.total_comprobantes_pendientes > 0" color="warning" class="mercury-badge-seg">
              {{ metricasFinanzas.total_comprobantes_pendientes }}
            </ion-badge>
          </ion-label>
        </ion-segment-button>
      </ion-segment>
    </div>

    <!-- VISTA 1: FINANZAS Y AUDITORÍA DE CUENTAS CORRIENTES (Subcomponente modular) -->
    <comp-cuentas-corrientes-seccion
      v-if="vistaActiva === 'finanzas'"
      ref="refFinanzas"
      @metricas_actualizadas="alActualizarMetricas"
    />

    <!-- VISTA 2: GESTIÓN DE CLIENTES COMERCIALES (ABM) -->
    <div v-else>
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
    </div>

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

    <!-- Toast para feedback de operaciones y errores -->
    <ion-toast
      :is-open="toast.visible.value"
      :message="toast.mensaje.value"
      :color="toast.color.value"
      duration="3000"
      @didDismiss="toast.ocultar"
    />
  </comp-page>
</template>

<script setup>
import { ref } from 'vue';
import { onIonViewWillEnter } from '@ionic/vue';
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
  IonAlert,
  IonSegment,
  IonSegmentButton,
  IonToast
} from '@ionic/vue';
import {
  businessOutline,
  createOutline,
  trashOutline,
  addOutline,
  walletOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import CompClienteModal from '../components/dominio/clientes/comp_cliente_modal.vue';
import CompCuentasCorrientesSeccion from '../components/dominio/clientes/comp_cuentas_corrientes_seccion.vue';

import { clientes_store } from '@/stores/clientes_store';
import { vibrar_toque } from '@/services/vibracion_service';
import { useToast } from '@/composables/useToast';

const toast = useToast();

const vistaActiva = ref('clientes');
const refFinanzas = ref(null);
const metricasFinanzas = ref({
  deuda_total: 0,
  clientes_con_deuda: 0,
  total_comprobantes_pendientes: 0
});

const busqueda = ref('');
const modalAbierto = ref(false);
const clienteSeleccionado = ref(null);
const guardando = ref(false);
const errorModal = ref('');

const mostrarAlertaBaja = ref(false);
const clienteAEliminar = ref(null);

const alActualizarMetricas = (nuevasMetricas) => {
  if (nuevasMetricas) {
    metricasFinanzas.value = nuevasMetricas;
  }
};

const alBuscar = (texto) => {
  clientes_store.establecer_busqueda(texto);
};

const recargar = async (event = null) => {
  if (vistaActiva.value === 'finanzas') {
    try {
      await refFinanzas.value?.cargarFinanzas();
    } finally {
      if (event?.target?.complete) event.target.complete();
    }
  } else {
    clientes_store.cargar(true, event);
  }
};

const alCambiarVista = () => {
  vibrar_toque();
  if (vistaActiva.value === 'finanzas') {
    refFinanzas.value?.cargarFinanzas();
  }
};

const abrirNuevo = () => {
  vibrar_toque();
  clienteSeleccionado.value = null;
  errorModal.value = '';
  modalAbierto.value = true;
};

const abrirEditar = (cliente) => {
  vibrar_toque();
  clienteSeleccionado.value = { ...cliente };
  errorModal.value = '';
  modalAbierto.value = true;
};

const cerrarModal = () => {
  modalAbierto.value = false;
  clienteSeleccionado.value = null;
  errorModal.value = '';
};

const guardarCliente = async (datos) => {
  guardando.value = true;
  errorModal.value = '';
  try {
    if (clienteSeleccionado.value?.id) {
      await clientes_store.actualizar_cliente(clienteSeleccionado.value.id, datos);
      toast.mostrarExito('Cliente actualizado con éxito.');
    } else {
      await clientes_store.crear_cliente(datos);
      toast.mostrarExito('¡Cliente creado exitosamente!');
    }
    cerrarModal();
  } catch (err) {
    errorModal.value = err.message || 'Error al guardar el cliente.';
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
    toast.mostrarExito('Cliente dado de baja exitosamente.');
  } catch (err) {
    toast.mostrarError(err.message || 'No se pudo dar de baja al cliente.');
  } finally {
    clienteAEliminar.value = null;
    mostrarAlertaBaja.value = false;
  }
};

onIonViewWillEnter(() => {
  clientes_store.cargar(true);
  if (vistaActiva.value === 'finanzas') {
    refFinanzas.value?.cargarFinanzas();
  }
});
</script>

<style scoped>
.mercury-segmento-contenedor {
  padding: 8px 12px;
}

.mercury-badge-seg {
  margin-left: 6px;
  font-size: 0.72rem;
  padding: 2px 5px;
}

.mercury-cliente-card {
  --padding-start: 12px;
  --padding-end: 8px;
  --padding-top: 10px;
  --padding-bottom: 10px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-icono-cliente {
  font-size: 1.8rem;
  color: var(--ion-color-primary);
  margin-right: 12px;
}

.mercury-cliente-cabecera {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 4px;
}

.mercury-cliente-cabecera h3 {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
  color: var(--ion-color-step-850, #1e293b);
}

.mercury-badge-cuit {
  font-size: 0.75rem;
  font-weight: 600;
  letter-spacing: 0.3px;
}

.mercury-cliente-direccion {
  margin: 2px 0 4px 0;
  font-size: 0.85rem;
  color: var(--ion-color-step-600, #64748b);
}

.mercury-cliente-contacto {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  font-size: 0.8rem;
  color: var(--ion-color-step-500, #94a3b8);
}

.mercury-botones-accion {
  display: flex;
  align-items: center;
}

.mercury-fab {
  margin-bottom: 56px;
  margin-right: 12px;
}
</style>