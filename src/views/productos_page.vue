<template>
  <comp-page titulo="Productos" :mostrar_actualizar="true" @actualizar="productos_store.cargar">
    <!-- Estado 1: Cargando datos (esqueleto) -->
    <comp-esqueleto v-if="productos_store.cargando" />

    <!-- Estado 2: Error al conectar con la API (con botón Reintentar) -->
    <comp-estado-error
      v-else-if="productos_store.error"
      :mensaje="productos_store.error"
      @reintentar="productos_store.cargar"
    />

    <!-- Estado 3: Respuesta exitosa pero vacía -->
    <comp-estado-vacio v-else-if="!productos_store.hay_productos" />

    <!-- Estado 4: Lista con datos reales del backend -->
    <template v-else>
      <div class="resumen-catalogo ion-padding-horizontal">
        <p class="resumen-texto">
          Total: <strong>{{ productos_store.resumen.total }}</strong> · 
          Disponibles: <strong>{{ productos_store.resumen.disponibles }}</strong>
        </p>
      </div>

      <ion-list>
        <ion-item v-for="p in productos_store.productos" :key="p.id">
          <ion-label>
            <h3>{{ p.nombre }}</h3>
            <p>{{ p.categoria_nombre }} · {{ p.descripcion }}</p>
            <p><strong>${{ p.precio }}</strong></p>
          </ion-label>
          <ion-badge slot="end" :color="p.disponible ? 'success' : 'medium'">
            {{ p.disponible ? 'Disponible' : 'Sin stock' }}
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
import { productos_store } from '@/stores/productos_store';

onMounted(() => {
  productos_store.cargar();
});
</script>

<style scoped>
.resumen-catalogo {
  padding-top: 8px;
  padding-bottom: 4px;
}

.resumen-texto {
  font-size: 0.85rem;
  color: var(--ion-color-step-600, #64748b);
  margin: 0;
}
</style>