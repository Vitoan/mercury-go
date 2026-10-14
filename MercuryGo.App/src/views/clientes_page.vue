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

    <!-- VISTA 1: FINANZAS Y AUDITORÍA DE CUENTAS CORRIENTES -->
    <div v-if="vistaActiva === 'finanzas'" class="mercury-finanzas-seccion">
      <!-- Tarjeta Resumen General -->
      <ion-card class="mercury-card-metricas">
        <ion-card-content>
          <div class="mercury-metricas-grid">
            <div class="mercury-metrica-caja">
              <span class="mercury-metrica-label">Deuda Total en Calle:</span>
              <strong class="mercury-metrica-valor text-warning">
                ${{ metricasFinanzas.deuda_total.toLocaleString('es-AR') }}
              </strong>
            </div>
            <div class="mercury-metrica-caja">
              <span class="mercury-metrica-label">Clientes con Saldo Deudor:</span>
              <strong class="mercury-metrica-valor">{{ metricasFinanzas.clientes_con_deuda }}</strong>
            </div>
          </div>
          <div v-if="metricasFinanzas.total_comprobantes_pendientes > 0" class="mercury-alerta-pendientes">
            <ion-icon :icon="timeOutline" color="warning" />
            <span>Hay <strong>{{ metricasFinanzas.total_comprobantes_pendientes }}</strong> comprobante(s) esperando tu aprobación.</span>
          </div>
        </ion-card-content>
      </ion-card>

      <!-- Spinner cargando finanzas -->
      <div v-if="cargandoFinanzas" class="mercury-finanzas-loading">
        <ion-spinner name="crescent" color="primary" />
        <p>Actualizando cuentas corrientes…</p>
      </div>

      <!-- Lista de Clientes con Estado Financiero -->
      <ion-list v-else class="mercury-finanzas-lista">
        <ion-card v-for="item in cuentasClientes" :key="item.cliente_id" class="mercury-card-cliente-cc">
          <ion-card-header>
            <div class="mercury-cc-cabecera">
              <div>
                <ion-card-title class="mercury-cc-titulo">{{ item.razon_social }}</ion-card-title>
                <ion-card-subtitle>CUIT: {{ item.cuit }} · {{ item.localidad || 'General' }}</ion-card-subtitle>
              </div>
              <ion-badge :color="item.saldo_deudor > 0 ? 'warning' : 'success'" class="mercury-badge-deuda">
                {{ item.saldo_deudor > 0 ? 'Debe: $' + item.saldo_deudor.toLocaleString('es-AR') : 'Al Día' }}
              </ion-badge>
            </div>
          </ion-card-header>
          <ion-card-content>
            <div class="mercury-cc-totales-fila">
              <span>Facturado: <strong>${{ item.total_facturado.toLocaleString('es-AR') }}</strong></span>
              <span>Acreditado: <strong class="text-success">${{ item.total_pagado.toLocaleString('es-AR') }}</strong></span>
            </div>

            <!-- Comprobantes pendientes de auditar de este cliente -->
            <div v-if="item.comprobantes_pendientes_conteo > 0" class="mercury-cc-comprobantes-box">
              <div class="mercury-box-titulo">
                <ion-icon :icon="cardOutline" color="warning" />
                <span>Comprobantes esperando aprobación ({{ item.comprobantes_pendientes_conteo }}):</span>
              </div>

              <div
                v-for="comp in item.comprobantes_pendientes"
                :key="comp.pedido_id"
                class="mercury-item-comprobante-fila"
              >
                <div class="mercury-comp-info">
                  <strong>Orden {{ comp.pedido_numero }}</strong>
                  <span>Monto: ${{ comp.total.toLocaleString('es-AR') }}</span>
                </div>
                <div class="mercury-comp-botones">
                  <ion-button
                    v-if="comp.comprobante_url"
                    size="small"
                    fill="outline"
                    color="tertiary"
                    @click="verArchivoComprobante(comp.comprobante_url)"
                  >
                    Ver Foto / PDF
                  </ion-button>
                  <ion-button
                    size="small"
                    color="success"
                    @click="aprobarComprobanteDirecto(comp)"
                  >
                    ✔ Aprobar
                  </ion-button>
                  <ion-button
                    size="small"
                    color="danger"
                    fill="outline"
                    @click="rechazarComprobanteDirecto(comp.pedido_id)"
                  >
                    ✖ Rechazar
                  </ion-button>
                </div>
              </div>
            </div>
          </ion-card-content>
        </ion-card>
      </ion-list>
    </div>

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
  IonAlert,
  IonSegment,
  IonSegmentButton,
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardSubtitle,
  IonCardContent,
  IonList,
  IonSpinner,
  IonToast,
  alertController
} from '@ionic/vue';
import {
  businessOutline,
  createOutline,
  trashOutline,
  addOutline,
  walletOutline,
  timeOutline,
  cardOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import CompClienteModal from '../components/dominio/clientes/comp_cliente_modal.vue';

import { clientes_store } from '@/stores/clientes_store';
import { pedidos_service } from '@/services/pedidos_service';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { resolver_url_imagen } from '@/utils/imagenes';
import { useToast } from '@/composables/useToast';

const toast = useToast();

const vistaActiva = ref('clientes');
const cargandoFinanzas = ref(false);
const cuentasClientes = ref([]);
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
    toast.mostrarExito('Cliente dado de baja exitosamente.');
  } catch (err) {
    toast.mostrarError(err.message || 'No se pudo dar de baja al cliente.');
  } finally {
    clienteAEliminar.value = null;
    mostrarAlertaBaja.value = false;
  }
};

const cargarFinanzas = async () => {
  cargandoFinanzas.value = true;
  try {
    const res = await pedidos_service.obtener_cuentas_corrientes_clientes();
    cuentasClientes.value = res.clientes || [];
    metricasFinanzas.value = res.metricas || {
      deuda_total: 0,
      clientes_con_deuda: 0,
      total_comprobantes_pendientes: 0
    };
  } catch (err) {
    console.error('Error al cargar cuentas corrientes:', err);
  } finally {
    cargandoFinanzas.value = false;
  }
};

const alCambiarVista = () => {
  vibrar_toque();
  if (vistaActiva.value === 'finanzas') {
    cargarFinanzas();
  }
};

const verArchivoComprobante = (urlRelativa) => {
  vibrar_toque();
  const urlCompleta = resolver_url_imagen(urlRelativa);
  if (urlCompleta) window.open(urlCompleta, '_blank');
};

const aprobarComprobanteDirecto = async (comp) => {
  const pedidoId = comp?.pedido_id || comp;
  const montoSugerido = comp?.total || 0;

  const alert = await alertController.create({
    header: 'Auditoría Bancaria',
    subHeader: `Orden ${comp?.pedido_numero || pedidoId}`,
    message: 'Ingresá el monto exacto acreditado en la cuenta de la empresa:',
    inputs: [
      {
        name: 'monto',
        type: 'number',
        placeholder: 'Monto acreditado',
        value: montoSugerido.toString(),
        min: 1
      }
    ],
    buttons: [
      {
        text: 'Cancelar',
        role: 'cancel'
      },
      {
        text: 'Aprobar Pago',
        handler: async (data) => {
          const rawMonto = data?.monto;
          if (!rawMonto) {
            toast.mostrarError('Por favor ingresá un monto válido mayor a 0.');
            return false;
          }

          const monto = parseFloat(String(rawMonto).replace(/\./g, '').replace(',', '.'));
          if (isNaN(monto) || monto <= 0) {
            toast.mostrarError('Por favor ingresá un monto válido mayor a 0.');
            return false;
          }

          try {
            vibrar_toque();
            await pedidos_service.aprobar_comprobante(pedidoId, monto);
            await vibrar_exito();
            toast.mostrarExito('¡Comprobante aprobado exitosamente!');
            await cargarFinanzas();
            return true;
          } catch (err) {
            await vibrar_error();
            toast.mostrarError(err.mensaje || err.message || 'No se pudo aprobar el comprobante.');
            return false;
          }
        }
      }
    ]
  });

  await alert.present();
};

const rechazarComprobanteDirecto = async (pedidoId) => {
  const alert = await alertController.create({
    header: 'Rechazar Comprobante',
    message: 'Ingresá el motivo del rechazo del comprobante de pago:',
    inputs: [
      {
        name: 'motivo',
        type: 'text',
        placeholder: 'Motivo de rechazo',
        value: 'Comprobante no acreditado en cuenta'
      }
    ],
    buttons: [
      {
        text: 'Cancelar',
        role: 'cancel'
      },
      {
        text: 'Rechazar Pago',
        role: 'destructive',
        handler: async (data) => {
          const motivo = data?.motivo?.trim();
          if (!motivo) {
            toast.mostrarError('Por favor especificá el motivo del rechazo.');
            return false;
          }

          try {
            vibrar_toque();
            await pedidos_service.rechazar_comprobante(pedidoId, motivo);
            await vibrar_exito();
            toast.mostrarExito('Comprobante rechazado correctamente.');
            await cargarFinanzas();
            return true;
          } catch (err) {
            await vibrar_error();
            toast.mostrarError(err.mensaje || err.message || 'No se pudo rechazar el comprobante.');
            return false;
          }
        }
      }
    ]
  });

  await alert.present();
};

onMounted(() => {
  clientes_store.cargar(true);
  cargarFinanzas();
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

/* Estilos de Finanzas y Auditoría de Cuentas Corrientes */
.mercury-segmento-contenedor {
  padding: 8px 12px;
}

.mercury-badge-seg {
  margin-left: 6px;
  font-size: 0.72rem;
  padding: 2px 5px;
}

.mercury-finanzas-seccion {
  padding: 0 12px 24px 12px;
}

.mercury-card-metricas {
  margin: 0 0 16px 0;
  border-radius: 12px;
  background: var(--ion-color-step-50, #f8fafc);
}

.mercury-metricas-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.mercury-metrica-caja {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.mercury-metrica-label {
  font-size: 0.8rem;
  color: var(--ion-color-step-600, #64748b);
}

.mercury-metrica-valor {
  font-size: 1.25rem;
  font-weight: 800;
}

.mercury-alerta-pendientes {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px dashed var(--ion-color-step-200, #e2e8f0);
  font-size: 0.88rem;
}

.mercury-finanzas-loading {
  text-align: center;
  padding: 30px;
  color: var(--ion-color-medium, #64748b);
}

.mercury-card-cliente-cc {
  margin: 0 0 14px 0;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.mercury-cc-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

.mercury-cc-titulo {
  font-size: 1.05rem;
  font-weight: 700;
}

.mercury-badge-deuda {
  font-size: 0.85rem;
  padding: 6px 10px;
  border-radius: 6px;
}

.mercury-cc-totales-fila {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
  margin-bottom: 10px;
}

.mercury-cc-comprobantes-box {
  margin-top: 10px;
  padding: 10px;
  border-radius: 8px;
  background: var(--ion-color-step-100, rgba(0, 0, 0, 0.04));
  border-left: 3px solid var(--ion-color-warning, #f59e0b);
}

.mercury-box-titulo {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  font-weight: 700;
  margin-bottom: 8px;
}

.mercury-item-comprobante-fila {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  padding: 8px 0;
  border-top: 1px dashed var(--ion-color-step-200, rgba(0, 0, 0, 0.08));
}

.mercury-comp-info {
  display: flex;
  flex-direction: column;
  font-size: 0.85rem;
}

.mercury-comp-botones {
  display: flex;
  gap: 6px;
}
</style>