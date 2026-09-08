<template>
  <comp-page titulo="Órdenes de Carga y Despacho" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Buscador con debounce en servidor -->
    <comp-buscador
      v-model="busqueda"
      placeholder="Buscar por remito (PED-...) o cliente…"
      @buscar="alBuscar"
    />

    <!-- Filtros de estado logístico -->
    <div class="mercury-filtros-estados">
      <ion-chip
        :color="estadoSeleccionado === '' ? 'primary' : 'medium'"
        :outline="estadoSeleccionado !== ''"
        @click="filtrarEstado('')"
      >
        <ion-label>Todos ({{ pedidos_store.total }})</ion-label>
      </ion-chip>
      <ion-chip
        :color="estadoSeleccionado === 'Pendiente' ? 'primary' : 'medium'"
        :outline="estadoSeleccionado !== 'Pendiente'"
        @click="filtrarEstado('Pendiente')"
      >
        <ion-label>Pendientes</ion-label>
      </ion-chip>
      <ion-chip
        :color="estadoSeleccionado === 'EnPreparacion' ? 'warning' : 'medium'"
        :outline="estadoSeleccionado !== 'EnPreparacion'"
        @click="filtrarEstado('EnPreparacion')"
      >
        <ion-label>En Depósito</ion-label>
      </ion-chip>
      <ion-chip
        :color="estadoSeleccionado === 'Despachado' ? 'primary' : 'medium'"
        :outline="estadoSeleccionado !== 'Despachado'"
        @click="filtrarEstado('Despachado')"
      >
        <ion-label>En Ruta</ion-label>
      </ion-chip>
      <ion-chip
        :color="estadoSeleccionado === 'Entregado' ? 'success' : 'medium'"
        :outline="estadoSeleccionado !== 'Entregado'"
        @click="filtrarEstado('Entregado')"
      >
        <ion-label>Entregados</ion-label>
      </ion-chip>
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

    <!-- Estado 4: Lista paginada con máquina de transiciones -->
    <comp-lista
      v-else
      :cargando="pedidos_store.cargando"
      :hay_mas="pedidos_store.hay_mas"
      :mostrados="pedidos_store.mostrados"
      :total="pedidos_store.total"
      @cargar_mas="pedidos_store.cargar_mas"
    >
      <ion-item v-for="p in pedidos_store.pedidos" :key="p.id" class="mercury-pedido-item">
        <ion-icon :icon="documentTextOutline" slot="start" class="mercury-icono-pedido" />
        <ion-label>
          <div class="mercury-pedido-cabecera">
            <span class="mercury-pedido-numero">{{ p.numero }}</span>
            <span class="mercury-pedido-total">${{ p.total.toLocaleString('es-AR') }}</span>
          </div>
          <h3 class="mercury-pedido-cliente">{{ p.cliente_razon_social }}</h3>
          <p class="mercury-pedido-fecha">📅 {{ formatear_fecha(p.fecha_pedido) }}</p>
        </ion-label>

        <!-- Badge de estado clickeable para abrir opciones de transición -->
        <ion-badge
          slot="end"
          :color="obtener_color_estado(p.estado)"
          class="mercury-badge-estado"
          @click="abrirOpcionesEstado(p)"
        >
          {{ formatear_nombre_estado(p.estado) }}
        </ion-badge>
      </ion-item>
    </comp-lista>

    <!-- Action Sheet para transiciones válidas de la máquina de estados -->
    <ion-action-sheet
      :is-open="mostrarActionSheet"
      header="Actualizar Estado Logístico de la Orden"
      :sub-header="pedidoSeleccionado ? `${pedidoSeleccionado.numero} · Estado actual: ${pedidoSeleccionado.estado}` : ''"
      :buttons="botonesTransicion"
      @didDismiss="mostrarActionSheet = false"
    />

    <!-- Toast para feedback de transiciones y errores -->
    <ion-toast
      :is-open="mostrarToast"
      :message="mensajeToast"
      :color="colorToast"
      duration="3000"
      @didDismiss="mostrarToast = false"
    />
  </comp-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import {
  IonItem,
  IonLabel,
  IonBadge,
  IonChip,
  IonIcon,
  IonActionSheet,
  IonToast
} from '@ionic/vue';
import { documentTextOutline } from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';

import { pedidos_store } from '@/stores/pedidos_store';

const busqueda = ref('');
const estadoSeleccionado = ref('');

const mostrarActionSheet = ref(false);
const pedidoSeleccionado = ref(null);

const mostrarToast = ref(false);
const mensajeToast = ref('');
const colorToast = ref('success');

const alBuscar = (texto) => {
  pedidos_store.establecer_busqueda(texto);
};

const filtrarEstado = (estado) => {
  estadoSeleccionado.value = estado;
  pedidos_store.establecer_estados(estado);
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

const formatear_nombre_estado = (estado) => {
  switch (estado) {
    case 'EnPreparacion': return 'En Depósito';
    case 'Despachado': return 'En Ruta';
    default: return estado;
  }
};

const abrirOpcionesEstado = (pedido) => {
  pedidoSeleccionado.value = pedido;
  mostrarActionSheet.value = true;
};

// Genera botones de transición válidos según el estado actual (máquina de estados):
const botonesTransicion = computed(() => {
  if (!pedidoSeleccionado.value) return [];
  const estadoActual = pedidoSeleccionado.value.estado;
  const botones = [];

  if (estadoActual === 'Pendiente') {
    botones.push({
      text: 'Confirmar Pedido (▶ Confirmado)',
      handler: () => ejecutarCambioEstado('Confirmado')
    });
    botones.push({
      text: 'Cancelar Pedido (✖ Cancelado)',
      role: 'destructive',
      handler: () => ejecutarCambioEstado('Cancelado')
    });
  } else if (estadoActual === 'Confirmado') {
    botones.push({
      text: 'Iniciar Carga en Depósito (▶ En Preparación)',
      handler: () => ejecutarCambioEstado('EnPreparacion')
    });
    botones.push({
      text: 'Cancelar Pedido (✖ Cancelado)',
      role: 'destructive',
      handler: () => ejecutarCambioEstado('Cancelado')
    });
  } else if (estadoActual === 'EnPreparacion') {
    botones.push({
      text: 'Despachar al Camión (▶ En Ruta)',
      handler: () => ejecutarCambioEstado('Despachado')
    });
    botones.push({
      text: 'Cancelar Pedido (✖ Cancelado)',
      role: 'destructive',
      handler: () => ejecutarCambioEstado('Cancelado')
    });
  } else if (estadoActual === 'Despachado') {
    botones.push({
      text: 'Confirmar Entrega en Destino (✔ Entregado)',
      handler: () => ejecutarCambioEstado('Entregado')
    });
    botones.push({
      text: 'Devolución / Cancelación (✖ Cancelado)',
      role: 'destructive',
      handler: () => ejecutarCambioEstado('Cancelado')
    });
  }

  botones.push({
    text: 'Cerrar',
    role: 'cancel'
  });

  return botones;
});

const ejecutarCambioEstado = async (nuevoEstado) => {
  if (!pedidoSeleccionado.value) return;
  try {
    await pedidos_store.cambiar_estado(pedidoSeleccionado.value.id, nuevoEstado);
    mensajeToast.value = `Orden ${pedidoSeleccionado.value.numero} actualizada a '${formatear_nombre_estado(nuevoEstado)}'.`;
    colorToast.value = 'success';
    mostrarToast.value = true;
  } catch (err) {
    mensajeToast.value = err.message || 'La API rechazó el cambio de estado.';
    colorToast.value = 'danger';
    mostrarToast.value = true;
  } finally {
    mostrarActionSheet.value = false;
  }
};

onMounted(() => {
  pedidos_store.cargar(true);
});
</script>

<style scoped>
.mercury-filtros-estados {
  display: flex;
  overflow-x: auto;
  padding: 0 12px 6px 12px;
  gap: 4px;
  scrollbar-width: none;
}

.mercury-filtros-estados::-webkit-scrollbar {
  display: none;
}

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
  color: #38bdf8;
  letter-spacing: 0.3px;
}

.mercury-pedido-total {
  font-weight: 700;
  font-size: 1rem;
  color: var(--ion-text-color, #f8fafc);
}

.mercury-pedido-cliente {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 600;
  color: var(--ion-text-color, #e2e8f0);
}

.mercury-pedido-fecha {
  margin: 3px 0 0 0;
  font-size: 0.78rem;
  color: var(--ion-color-step-600, #94a3b8);
}

.mercury-badge-estado {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 5px 8px;
  border-radius: 6px;
  cursor: pointer;
}
</style>