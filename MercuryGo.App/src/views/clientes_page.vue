<template>
  <comp-page titulo="Clientes Comerciales" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Acción rápida de Nuevo Cliente en toolbar para Admin -->
    <template #acciones>
      <ion-button
        v-if="sesion_store.es_admin && vistaActiva === 'clientes'"
        title="Nuevo Cliente Comercial"
        class="mercury-btn-toolbar-nuevo"
        @click="abrirNuevo"
      >
        <ion-icon slot="icon-only" :icon="addOutline" />
      </ion-button>
    </template>

    <!-- Segmentos para alternar entre Gestión de Clientes y Cuentas Corrientes/Deuda -->
    <div class="mercury-segmento-contenedor">
      <ion-segment v-model="vistaActiva" @ionChange="alCambiarVista">
        <ion-segment-button value="clientes">
          <ion-icon :icon="businessOutline" />
          <ion-label>
            Clientes
            <ion-badge v-if="clientes_store.total > 0" color="medium" class="mercury-badge-seg">
              {{ clientes_store.total }}
            </ion-badge>
          </ion-label>
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
    <div v-else class="mercury-contenedor-clientes">
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
        <ion-item-sliding v-for="c in clientes_store.clientes" :key="c.id" :disabled="!sesion_store.es_admin">
          <ion-item class="mercury-cliente-card">
            <!-- Avatar con Iniciales Corporativas Dinámicas -->
            <ion-avatar slot="start" class="mercury-cliente-avatar">
              <span class="mercury-avatar-iniciales">{{ obtenerIniciales(c.razon_social) }}</span>
            </ion-avatar>

            <ion-label>
              <div class="mercury-cliente-cabecera">
                <h3>{{ c.razon_social }}</h3>
                <ion-badge color="primary" class="mercury-badge-cuit">CUIT {{ c.cuit }}</ion-badge>
              </div>

              <!-- Dirección / Localidad -->
              <p v-if="c.localidad || c.direccion" class="mercury-cliente-direccion">
                <ion-icon :icon="locationOutline" class="mercury-mini-icono" />
                <span>{{ c.direccion ? c.direccion + ' · ' : '' }}{{ c.localidad || 'Zona Central' }}</span>
              </p>

              <!-- Accesos Rápidos de Contacto -->
              <div class="mercury-cliente-contacto">
                <a
                  v-if="c.telefono"
                  :href="'tel:' + c.telefono"
                  class="mercury-chip-contacto"
                  title="Llamar al cliente"
                  @click.stop
                >
                  <ion-icon :icon="callOutline" />
                  <span>{{ c.telefono }}</span>
                </a>
                <a
                  v-if="c.email"
                  :href="'mailto:' + c.email"
                  class="mercury-chip-contacto"
                  title="Enviar correo"
                  @click.stop
                >
                  <ion-icon :icon="mailOutline" />
                  <span>{{ c.email }}</span>
                </a>
              </div>
            </ion-label>

            <!-- Botones de acción directa (Solo Admin) -->
            <ion-buttons v-if="sesion_store.es_admin" slot="end" class="mercury-botones-accion">
              <ion-button fill="clear" color="primary" title="Editar Cliente" @click.stop="abrirEditar(c)">
                <ion-icon slot="icon-only" :icon="createOutline" />
              </ion-button>
              <ion-button fill="clear" color="danger" title="Dar de baja" @click.stop="confirmarBaja(c)">
                <ion-icon slot="icon-only" :icon="trashOutline" />
              </ion-button>
            </ion-buttons>
          </ion-item>

          <!-- Opciones al deslizar (Mobile gesture - Solo Admin) -->
          <ion-item-options v-if="sesion_store.es_admin" side="end">
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

        <!-- Espaciador para que el último elemento nunca quede tapado por el botón flotante -->
        <div v-if="sesion_store.es_admin" class="mercury-espaciador-fab"></div>
      </comp-lista>
    </div>

    <!-- Botón flotante para registrar un nuevo cliente B2B (Solo Admin) -->
    <ion-fab
      v-if="sesion_store.es_admin && vistaActiva === 'clientes'"
      slot="fixed"
      vertical="bottom"
      horizontal="end"
      class="mercury-fab-contenedor"
    >
      <ion-fab-button
        color="primary"
        class="mercury-fab-admin"
        title="Nuevo Cliente Comercial"
        aria-label="Registrar Nuevo Cliente"
        @click="abrirNuevo"
      >
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
  IonAvatar,
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
  walletOutline,
  callOutline,
  mailOutline,
  locationOutline
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
import { sesion_store } from '@/stores/sesion_store';
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

const obtenerIniciales = (nombre) => {
  if (!nombre) return 'CL';
  const palabras = nombre.trim().split(/\s+/).filter(Boolean);
  if (palabras.length >= 2) {
    return (palabras[0][0] + palabras[1][0]).toUpperCase();
  }
  return nombre.slice(0, 2).toUpperCase();
};

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
  padding: 2px 6px;
  border-radius: 10px;
}

.mercury-contenedor-clientes {
  position: relative;
  min-height: 100%;
}

.mercury-cliente-card {
  --padding-start: 12px;
  --padding-end: 8px;
  --padding-top: 10px;
  --padding-bottom: 10px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-cliente-avatar {
  width: 44px;
  height: 44px;
  min-width: 44px;
  margin-right: 12px;
  background: linear-gradient(135deg, var(--ion-color-primary), var(--ion-color-primary-shade));
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(var(--ion-color-primary-rgb), 0.25);
}

.mercury-avatar-iniciales {
  color: #ffffff;
  font-weight: 700;
  font-size: 0.95rem;
  letter-spacing: 0.5px;
}

.mercury-cliente-cabecera {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 4px;
}

.mercury-cliente-cabecera h3 {
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
  color: var(--ion-color-step-850, #1e293b);
  line-height: 1.3;
}

.mercury-badge-cuit {
  font-size: 0.72rem;
  font-weight: 600;
  letter-spacing: 0.3px;
  white-space: nowrap;
}

.mercury-cliente-direccion {
  margin: 3px 0 6px 0;
  font-size: 0.85rem;
  color: var(--ion-color-step-600, #64748b);
  display: flex;
  align-items: center;
  gap: 4px;
}

.mercury-mini-icono {
  font-size: 0.9rem;
  color: var(--ion-color-primary);
  flex-shrink: 0;
}

.mercury-cliente-contacto {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 4px;
}

.mercury-chip-contacto {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 0.78rem;
  color: var(--ion-color-step-700, #475569);
  background: var(--ion-color-step-100, rgba(0, 0, 0, 0.04));
  padding: 3px 8px;
  border-radius: 6px;
  text-decoration: none;
  transition: background 0.2s ease, color 0.2s ease;
}

.mercury-chip-contacto:hover,
.mercury-chip-contacto:active {
  background: var(--ion-color-primary-tint, rgba(var(--ion-color-primary-rgb), 0.15));
  color: var(--ion-color-primary);
}

.mercury-chip-contacto ion-icon {
  font-size: 0.85rem;
  color: var(--ion-color-primary);
}

.mercury-botones-accion {
  display: flex;
  align-items: center;
}

.mercury-btn-toolbar-nuevo {
  --color: var(--ion-color-primary-contrast, #ffffff);
}

.mercury-fab-contenedor {
  margin-bottom: calc(56px + var(--ion-safe-area-bottom, 0px) + 12px);
  margin-right: 16px;
  z-index: 999;
}

.mercury-fab-admin {
  --box-shadow: 0 4px 16px rgba(var(--ion-color-primary-rgb), 0.45);
  transition: transform 0.2s ease;
}

.mercury-fab-admin:active {
  transform: scale(0.92);
}

.mercury-espaciador-fab {
  height: 80px;
}
</style>
