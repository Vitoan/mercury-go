<template>
  <comp-page titulo="Pedidos" :mostrar_actualizar="true" @actualizar="pedidos_store.cargar">
    <!-- Estado 1: Cargando datos (esqueleto) -->
    <comp-esqueleto v-if="pedidos_store.cargando" />

    <!-- Estado 2: Error al conectar con la API (con botón Reintentar) -->
    <comp-estado-error
      v-else-if="pedidos_store.error"
      :mensaje="pedidos_store.error"
      @reintentar="pedidos_store.cargar"
    />

    <!-- Estado 3: Respuesta exitosa pero vacía -->
    <comp-estado-vacio v-else-if="!pedidos_store.hay_pedidos" />

    <!-- Estado 4: Lista con datos reales del backend -->
    <template v-else>
      <div class="resumen-seccion ion-padding-horizontal">
        <p class="resumen-texto">
          Total de pedidos: <strong>{{ pedidos_store.total }}</strong>
        </p>
      </div>

      <ion-list>
        <ion-item v-for="p in pedidos_store.pedidos" :key="p.id">
          <ion-label>
            <h3>{{ p.numero }} · {{ p.cliente_razon_social }}</h3>
            <p>Fecha: {{ formatear_fecha(p.fecha_pedido) }}</p>
            <p><strong>${{ p.total.toLocaleString('es-AR') }}</strong></p>
          </ion-label>
          <ion-badge slot="end" :color="obtener_color_estado(p.estado)">
            {{ p.estado }}
          </ion-badge>
        </ion-item>
      </ion-list>
    </template>
  </comp-page>
</template>

<script setup>
import { onMounted } from 'vue';
import { IonList, IonItem, IonLabel, IonBadge } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import { pedidos_store } from '@/stores/pedidos_store';

const formatear_fecha = (fechaStr) => {
  if (!fechaStr) return '';
  const d = new Date(fechaStr);
  return d.toLocaleDateString('es-AR');
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

onMounted(() => {
  pedidos_store.cargar();
});
</script>

<style scoped>
.resumen-seccion {
  padding-top: 8px;
  padding-bottom: 4px;
}

.resumen-texto {
  font-size: 0.85rem;
  color: var(--ion-color-step-600, #64748b);
  margin: 0;
}
</style>