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
        :cargar_mas="clientes_store.cargar_mas"
        @cargar_mas="clientes_store.cargar_mas"
      >
        <ion-item-sliding v-for="c in clientes_store.clientes" :key="c.id" :disabled="!sesion_store.es_admin">
          <ion-item class="mercury-cliente-card">
            <!-- Avatar con Iniciales Corporativas Dinámicas -->
            <ion-avatar slot="start" class="mercury-cliente-avatar">
              <span class="mercury-avatar-iniciales">{{ obtenerIniciales(c.razon_social) }}</span>
            </ion-avatar>

            <!-- Contenido Principal: Fila de Nombre, Fila de CUIT, Dirección y Contacto -->
            <ion-label class="mercury-cliente-info">
              <!-- Nombre / Razón Social a ancho completo para máxima legibilidad -->
              <h3 class="mercury-cliente-nombre">{{ c.razon_social }}</h3>

              <!-- Placa / Chip de CUIT destacada y claramente legible -->
              <div class="mercury-cuit-contenedor">
                <span class="mercury-placa-cuit">
                  <ion-icon :icon="cardOutline" class="mercury-cuit-icono" />
                  <strong class="mercury-cuit-etiqueta">CUIT</strong>
                  <span class="mercury-cuit-numero">{{ c.cuit }}</span>
                </span>
              </div>

              <!-- Dirección / Localidad -->
              <p v-if="c.localidad || c.direccion" class="mercury-cliente-direccion">
                <ion-icon :icon="locationOutline" class="mercury-mini-icono" />
                <span>{{ c.direccion ? c.direccion + ' · ' : '' }}{{ c.localidad || 'Zona Central' }}</span>
              </p>

              <!-- Accesos Rápidos de Contacto -->
              <div v-if="c.telefono || c.email" class="mercury-cliente-contacto">
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

    <!-- Botón flotante para registrar un nuevo cliente B2B (Solo Admin - Fijo en Esquina Inferior Derecha) -->
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
  locationOutline,
  cardOutline
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
    await clientes_store.guardar_cliente(datos, clienteSeleccionado.value?.id || null);
    if (clienteSeleccionado.value?.id) {
      toast.mostrarExito('Cliente actualizado con éxito.');
    } else {
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
  --padding-end: 12px;
  --padding-top: 12px;
  --padding-bottom: 12px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-cliente-avatar {
  width: 46px;
  height: 46px;
  min-width: 46px;
  margin-right: 12px;
  background: linear-gradient(135deg, var(--ion-color-primary, #2563eb), var(--ion-color-primary-shade, #1d4ed8));
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(var(--ion-color-primary-rgb, 37, 99, 235), 0.3);
}

.mercury-avatar-iniciales {
  color: #ffffff;
  font-weight: 700;
  font-size: 1rem;
  letter-spacing: 0.5px;
}

.mercury-cliente-info {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

/* Nombre comercial destacado con alto contraste */
.mercury-cliente-nombre {
  margin: 0 0 2px 0;
  font-size: 1.05rem;
  font-weight: 700;
  color: var(--ion-text-color, #0f172a);
  line-height: 1.35;
  word-break: break-word;
}

/* Placa dedicada de CUIT */
.mercury-cuit-contenedor {
  display: flex;
  align-items: center;
  margin-bottom: 2px;
}

.mercury-placa-cuit {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 0.82rem;
  font-weight: 500;
  color: var(--ion-color-primary, #2563eb);
  background: rgba(var(--ion-color-primary-rgb, 37, 99, 235), 0.08);
  border: 1px solid rgba(var(--ion-color-primary-rgb, 37, 99, 235), 0.2);
  padding: 2px 8px;
  border-radius: 6px;
  letter-spacing: 0.3px;
}

.mercury-cuit-icono {
  font-size: 0.95rem;
}

.mercury-cuit-etiqueta {
  font-weight: 700;
  font-size: 0.76rem;
  letter-spacing: 0.5px;
}

.mercury-cuit-numero {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-weight: 600;
}

.mercury-cliente-direccion {
  margin: 2px 0 4px 0;
  font-size: 0.86rem;
  color: var(--ion-color-step-600, #475569);
  display: flex;
  align-items: center;
  gap: 4px;
  line-height: 1.3;
}

.mercury-mini-icono {
  font-size: 0.95rem;
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
  gap: 5px;
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--ion-color-step-750, #334155);
  background: var(--ion-color-step-100, rgba(0, 0, 0, 0.04));
  border: 1px solid var(--ion-color-step-200, rgba(0, 0, 0, 0.08));
  padding: 4px 10px;
  border-radius: 8px;
  text-decoration: none;
  transition: all 0.2s ease;
}

.mercury-chip-contacto:hover,
.mercury-chip-contacto:active {
  background: rgba(var(--ion-color-primary-rgb, 37, 99, 235), 0.12);
  color: var(--ion-color-primary, #2563eb);
  border-color: rgba(var(--ion-color-primary-rgb, 37, 99, 235), 0.3);
}

.mercury-chip-contacto ion-icon {
  font-size: 0.9rem;
  color: var(--ion-color-primary);
}

.mercury-btn-toolbar-nuevo {
  --color: var(--ion-color-primary-contrast, #ffffff);
}

/* Botón flotante anclado estrictamente en la esquina inferior derecha */
.mercury-fab-contenedor {
  position: fixed !important;
  bottom: calc(64px + var(--ion-safe-area-bottom, env(safe-area-inset-bottom, 0px))) !important;
  right: 18px !important;
  margin: 0 !important;
  z-index: 1000 !important;
}

.mercury-fab-admin {
  --background: var(--ion-color-primary, #2563eb);
  --color: var(--ion-color-primary-contrast, #ffffff);
  --box-shadow: 0 6px 20px rgba(0, 0, 0, 0.35);
  width: 56px;
  height: 56px;
  transition: transform 0.2s ease;
}

.mercury-fab-admin:active {
  transform: scale(0.92);
}

.mercury-espaciador-fab {
  height: 90px;
}
</style>
