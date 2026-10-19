<template>
  <comp-page titulo="Órdenes de Carga y Despacho" :mostrar_actualizar="true" @actualizar="recargar">
    <template #acciones>
      <ion-button v-if="!offline_store.sin_conexion" title="Escanear Código QR de Remito" @click="escanearRemitoQr">
        <ion-icon slot="icon-only" :icon="qrCodeOutline" />
      </ion-button>
      <ion-button v-if="sesion_store.es_admin && !offline_store.sin_conexion" title="Nuevo Pedido Asistido" @click="workflow.mostrarModalNuevoPedido.value = true">
        <ion-icon slot="icon-only" :icon="addCircleOutline" />
      </ion-button>
    </template>

    <!-- Aviso de modo sin conexión (Semana 8) -->
    <div v-if="offline_store.sin_conexion" class="ion-padding-horizontal ion-padding-top">
      <ion-note color="warning" class="mercury-offline-banner">
        📡 <strong>Sin conexión:</strong> estás viendo los datos guardados en tu teléfono. Podés cambiar el estado de un pedido (por ejemplo, registrar entrega en destino); se enviará solo cuando vuelva la red.
      </ion-note>
    </div>

    <!-- Buscador con debounce en servidor -->
    <comp-buscador
      v-if="!offline_store.sin_conexion"
      v-model="busqueda"
      placeholder="Buscar por remito (PED-...) o cliente…"
      @buscar="alBuscar"
    />

    <!-- Fila de Chips de Estado Logístico con Conteo Real e Iconos -->
    <div v-if="!offline_store.sin_conexion" class="mercury-seccion-chips">
      <div class="mercury-chips-scroll">
        <ion-chip
          :color="estadoSeleccionado === '' ? 'primary' : 'medium'"
          :outline="estadoSeleccionado !== ''"
          class="mercury-chip"
          @click="filtrarEstado('')"
        >
          <ion-icon :icon="layersOutline" />
          <ion-label>Todos · {{ pedidos_store.total }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="estadoSeleccionado === 'Pendiente' ? 'primary' : 'medium'"
          :outline="estadoSeleccionado !== 'Pendiente'"
          class="mercury-chip"
          @click="filtrarEstado('Pendiente')"
        >
          <ion-icon :icon="timeOutline" />
          <ion-label>Pendientes · {{ obtenerConteo('Pendiente') }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="estadoSeleccionado === 'EnPreparacion' ? 'warning' : 'medium'"
          :outline="estadoSeleccionado !== 'EnPreparacion'"
          class="mercury-chip"
          @click="filtrarEstado('EnPreparacion')"
        >
          <ion-icon :icon="cubeOutline" />
          <ion-label>En Depósito · {{ obtenerConteo('EnPreparacion') }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="estadoSeleccionado === 'Despachado' ? 'primary' : 'medium'"
          :outline="estadoSeleccionado !== 'Despachado'"
          class="mercury-chip"
          @click="filtrarEstado('Despachado')"
        >
          <ion-icon :icon="trailSignOutline" />
          <ion-label>En Ruta · {{ obtenerConteo('Despachado') }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="estadoSeleccionado === 'EnPuerta' ? 'secondary' : 'medium'"
          :outline="estadoSeleccionado !== 'EnPuerta'"
          class="mercury-chip"
          @click="filtrarEstado('EnPuerta')"
        >
          <ion-icon :icon="homeOutline" />
          <ion-label>En Puerta · {{ obtenerConteo('EnPuerta') }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="estadoSeleccionado === 'Entregado' ? 'success' : 'medium'"
          :outline="estadoSeleccionado !== 'Entregado'"
          class="mercury-chip"
          @click="filtrarEstado('Entregado')"
        >
          <ion-icon :icon="checkmarkCircleOutline" />
          <ion-label>Entregados · {{ obtenerConteo('Entregado') }}</ion-label>
        </ion-chip>

        <ion-chip
          v-if="obtenerConteo('Cancelado') > 0"
          :color="estadoSeleccionado === 'Cancelado' ? 'danger' : 'medium'"
          :outline="estadoSeleccionado !== 'Cancelado'"
          class="mercury-chip"
          @click="filtrarEstado('Cancelado')"
        >
          <ion-icon :icon="closeCircleOutline" />
          <ion-label>Cancelados · {{ obtenerConteo('Cancelado') }}</ion-label>
        </ion-chip>
      </div>
    </div>

    <!-- Estado 1: Cargando primera página -->
    <comp-esqueleto v-if="pedidos_store.cargando && !pedidos_store.hay_pedidos" />

    <!-- Estado 2: Error al conectar con la API -->
    <comp-estado-error
      v-else-if="pedidos_store.error && !pedidos_store.hay_pedidos"
      :mensaje="pedidos_store.error"
      @reintentar="recargar"
    />

    <!-- Estado 3: Respuesta exitosa pero vacía -->
    <comp-estado-vacio
      v-else-if="!pedidos_store.hay_pedidos"
      mensaje="No se encontraron órdenes de carga con los filtros aplicados."
    />

    <!-- Estado 4: Lista paginada con interacción contextual según rol -->
    <comp-lista
      v-else
      :cargando="pedidos_store.cargando"
      :hay_mas="pedidos_store.hay_mas"
      :mostrados="pedidos_store.mostrados"
      :total="pedidos_store.total"
      @cargar_mas="pedidos_store.cargar_mas"
    >
      <ion-item
        v-for="p in pedidos_store.pedidos"
        :key="p.id"
        class="mercury-pedido-item"
        button
        @click="workflow.alTocarPedido(p)"
      >
        <ion-icon :icon="documentTextOutline" slot="start" class="mercury-icono-pedido" />
        <ion-label>
          <div class="mercury-pedido-cabecera">
            <span class="mercury-pedido-numero">{{ p.numero }}</span>
            <span class="mercury-pedido-total">${{ p.total.toLocaleString('es-AR') }}</span>
          </div>
          <h3 class="mercury-pedido-cliente">{{ p.cliente_razon_social }}</h3>
          <p class="mercury-pedido-fecha">📅 {{ formatear_fecha(p.fecha_pedido) }}</p>

          <!-- Badges de estado de Picking, Comprobante y Cobro Chofer -->
          <div v-if="tieneSubEtiquetas(p)" class="mercury-pedido-sub-tags">
            <span v-if="p.observaciones?.includes('[Picking: INCOMPLETO')" class="mercury-sub-tag tag-warning">
              ⚠ Faltantes detectados
            </span>
            <span v-else-if="p.observaciones?.includes('[Picking: COMPLETO')" class="mercury-sub-tag tag-success">
              ✔ Picking completo
            </span>
            <span v-if="p.observaciones?.includes('[Comprobante de pago: APROBADO')" class="mercury-sub-tag tag-success">
              ✔ Pago aprobado
            </span>
            <span v-else-if="p.observaciones?.includes('[Comprobante de pago: RECHAZADO')" class="mercury-sub-tag tag-danger">
              ✖ Pago rechazado
            </span>
            <span v-else-if="p.observaciones?.includes('[Comprobante de pago:')" class="mercury-sub-tag tag-warning">
              ⏳ Comprobante subido (esperando aprobación)
            </span>
            <span v-if="p.observaciones?.includes('[Cobro Chofer:')" class="mercury-sub-tag tag-success">
              💵 Cobrado en destino
            </span>
          </div>
        </ion-label>

        <!-- Badge de estado clickeable para abrir opciones de transición y badge offline -->
        <div slot="end" class="mercury-pedido-estado-columna">
          <ion-badge
            :color="obtener_color_estado(p.estado)"
            class="mercury-badge-estado"
            @click.stop="workflow.abrirOpcionesEstado(p)"
          >
            {{ workflow.formatear_nombre_estado(p.estado) }}
          </ion-badge>
          <ion-badge v-if="p.sin_enviar" color="warning" class="mercury-badge-sin-enviar">
            Sin enviar
          </ion-badge>
        </div>
      </ion-item>
    </comp-lista>

    <!-- Modal de Detalle Completo de Pedido (Ver, PDF y Edición Admin) -->
    <comp-detalle-pedido-modal
      :abierto="workflow.mostrarModalDetalle.value"
      :pedido-id="workflow.pedidoSeleccionado.value?.id"
      @cerrar="workflow.mostrarModalDetalle.value = false"
      @pedido_actualizado="workflow.alActualizarPedidoDetalle"
    />

    <!-- Action Sheet para transiciones válidas de la máquina de estados -->
    <ion-action-sheet
      :is-open="workflow.mostrarActionSheet.value"
      header="Actualizar Estado Logístico de la Orden"
      :sub-header="workflow.pedidoSeleccionado.value ? `${workflow.pedidoSeleccionado.value.numero} · Estado actual: ${workflow.pedidoSeleccionado.value.estado}` : ''"
      :buttons="workflow.botonesTransicion.value"
      @didDismiss="workflow.mostrarActionSheet.value = false"
    />

    <!-- Modal de Carga de Comprobante de Pago (Cámara / PDF) -->
    <comp-comprobante-modal
      :abierto="workflow.mostrarModalComprobante.value"
      :pedido="workflow.pedidoSeleccionado.value"
      @cerrar="workflow.mostrarModalComprobante.value = false"
      @comprobante_subido="workflow.alSubirComprobante"
    />

    <!-- Modal de Cobro en Destino para Chofer -->
    <comp-cobro-chofer-modal
      :abierto="workflow.mostrarModalCobro.value"
      :pedido="workflow.pedidoSeleccionado.value"
      @cerrar="workflow.mostrarModalCobro.value = false"
      @cobro_registrado="workflow.alRegistrarCobro"
    />

    <!-- Modal de Picking en Depósito (Operario / Admin) -->
    <comp-picking-modal
      :abierto="workflow.mostrarModalPicking.value"
      :pedido="workflow.pedidoSeleccionado.value"
      @cerrar="workflow.mostrarModalPicking.value = false"
      @picking_completado="workflow.alCompletarPicking"
    />

    <!-- Modal de Creación de Pedido Asistido / Teléfono (Admin) -->
    <comp-pedido-admin-modal
      :abierto="workflow.mostrarModalNuevoPedido.value"
      @cerrar="workflow.mostrarModalNuevoPedido.value = false"
      @pedido_creado="workflow.alCrearPedidoAdmin"
    />

    <!-- Modal de Entrega Fallida (Chofer / Admin) -->
    <comp-entrega-fallida-modal
      :abierto="workflow.mostrarModalEntregaFallida.value"
      :pedido="workflow.pedidoSeleccionado.value"
      @cerrar="workflow.mostrarModalEntregaFallida.value = false"
      @entrega_fallida_registrada="workflow.alRegistrarEntregaFallida"
    />

    <!-- Modal de Entrega Parcial / Rechazo (Chofer / Admin) -->
    <comp-entrega-parcial-modal
      :abierto="workflow.mostrarModalEntregaParcial.value"
      :pedido="workflow.pedidoSeleccionado.value"
      @cerrar="workflow.mostrarModalEntregaParcial.value = false"
      @entrega_parcial_registrada="workflow.alRegistrarEntregaParcial"
    />

    <!-- Toast para feedback de transiciones y errores -->
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
import { ref, watch } from 'vue';
import {
  IonItem,
  IonLabel,
  IonBadge,
  IonChip,
  IonIcon,
  IonButton,
  IonActionSheet,
  IonToast,
  IonNote,
  onIonViewWillEnter
} from '@ionic/vue';
import {
  layersOutline,
  timeOutline,
  cubeOutline,
  trailSignOutline,
  checkmarkCircleOutline,
  closeCircleOutline,
  documentTextOutline,
  qrCodeOutline,
  addCircleOutline,
  homeOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import CompComprobanteModal from '../components/dominio/pedidos/comp_comprobante_modal.vue';
import CompPickingModal from '../components/dominio/pedidos/comp_picking_modal.vue';
import CompPedidoAdminModal from '../components/dominio/pedidos/comp_pedido_admin_modal.vue';
import CompDetallePedidoModal from '../components/dominio/pedidos/comp_detalle_pedido_modal.vue';
import CompCobroChoferModal from '../components/dominio/pedidos/comp_cobro_chofer_modal.vue';
import CompEntregaFallidaModal from '../components/dominio/pedidos/comp_entrega_fallida_modal.vue';
import CompEntregaParcialModal from '../components/dominio/pedidos/comp_entrega_parcial_modal.vue';

import { usePedidosStore } from '@/stores/pedidos_store';
import { useSesionStore } from '@/stores/sesion_store';
import { use_offline_store } from '@/stores/offline_store';
import { useToast } from '@/composables/useToast';
import { usePedidosWorkflow } from '@/composables/usePedidosWorkflow';
import { useEscaneoQr } from '@/composables/useEscaneoQr';

const pedidos_store = usePedidosStore();
const sesion_store = useSesionStore();
const offline_store = use_offline_store();
const toast = useToast();
const workflow = usePedidosWorkflow(toast);

const busqueda = ref('');
const estadoSeleccionado = ref('');

const { escanearRemitoQr } = useEscaneoQr({
  busquedaRef: busqueda,
  estadoSeleccionadoRef: estadoSeleccionado,
  alEncontrarPedido: workflow.alTocarPedido,
  toast
});

const alBuscar = (texto) => {
  pedidos_store.establecer_busqueda(texto);
};

const filtrarEstado = (estado) => {
  estadoSeleccionado.value = estado;
  pedidos_store.establecer_estados(estado);
};

const obtenerConteo = (estadoNombre) => {
  const item = pedidos_store.conteos_por_estado.find(
    (c) => c.estado?.toLowerCase() === estadoNombre?.toLowerCase()
  );
  return item ? item.cantidad : 0;
};

const recargar = (event = null) => {
  pedidos_store.cargar(true, event);
};

const formatear_fecha = (fechaStr) => {
  if (!fechaStr) return '';
  const d = new Date(fechaStr);
  return d.toLocaleDateString('es-AR') + ' · ' + d.toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' });
};

const obtener_color_estado = (estado) => {
  switch (estado) {
    case 'Entregado': return 'success';
    case 'Despachado': return 'primary';
    case 'EnPreparacion': return 'warning';
    case 'Confirmado': return 'tertiary';
    case 'Cancelado': return 'danger';
    default: return 'medium'; // Pendiente
  }
};

const tieneSubEtiquetas = (pedido) => {
  return pedido?.observaciones && (
    pedido.observaciones.includes('[Picking:') ||
    pedido.observaciones.includes('[Comprobante de pago:')
  );
};

onIonViewWillEnter(() => {
  busqueda.value = pedidos_store.filtros.busqueda || '';
  estadoSeleccionado.value = pedidos_store.filtros.estados || '';
  pedidos_store.cargar(true);
});

watch(
  () => sesion_store.usuario?.id,
  () => {
    busqueda.value = '';
    estadoSeleccionado.value = '';
    pedidos_store.limpiar_filtros();
    pedidos_store.cargar(true);
  }
);
</script>

<style scoped>
.mercury-pedido-item {
  --padding-start: 12px;
  --padding-end: 12px;
  --padding-top: 10px;
  --padding-bottom: 10px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-icono-pedido {
  font-size: 1.6rem;
  color: var(--ion-color-primary, #38bdf8);
}

.mercury-pedido-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2px;
}

.mercury-pedido-numero {
  font-weight: 700;
  font-size: 0.95rem;
  color: var(--mercury-accent);
  letter-spacing: 0.3px;
}

.mercury-pedido-total {
  font-weight: 700;
  font-size: 1rem;
  color: var(--mercury-text-title);
}

.mercury-pedido-cliente {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 600;
  color: var(--mercury-text-body);
}

.mercury-pedido-fecha {
  margin: 3px 0 0 0;
  font-size: 0.78rem;
  color: var(--mercury-text-muted);
}

.mercury-badge-estado {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 5px 8px;
  border-radius: 6px;
  cursor: pointer;
}

.mercury-pedido-sub-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 5px;
}

.mercury-sub-tag {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 2px 7px;
  border-radius: 6px;
  display: inline-block;
}

.tag-warning {
  background: rgba(234, 179, 8, 0.15);
  color: #b45309;
  border: 1px solid rgba(234, 179, 8, 0.3);
}

.tag-success {
  background: rgba(22, 163, 74, 0.12);
  color: #15803d;
  border: 1px solid rgba(22, 163, 74, 0.3);
}

.tag-info {
  background: rgba(2, 132, 199, 0.12);
  color: #0284c7;
  border: 1px solid rgba(2, 132, 199, 0.3);
}
</style>