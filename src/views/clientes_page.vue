<template>
  <comp-page titulo="Clientes" :mostrar_actualizar="true" @actualizar="clientes_store.cargar">
    <!-- Estado 1: Cargando datos (esqueleto) -->
    <comp-esqueleto v-if="clientes_store.cargando" />

    <!-- Estado 2: Error al conectar con la API (con botón Reintentar) -->
    <comp-estado-error
      v-else-if="clientes_store.error"
      :mensaje="clientes_store.error"
      @reintentar="clientes_store.cargar"
    />

    <!-- Estado 3: Respuesta exitosa pero vacía -->
    <comp-estado-vacio v-else-if="!clientes_store.hay_clientes" />

    <!-- Estado 4: Lista con datos reales del backend -->
    <template v-else>
      <div class="resumen-seccion ion-padding-horizontal">
        <p class="resumen-texto">
          Total de cuentas activas: <strong>{{ clientes_store.total }}</strong>
        </p>
      </div>

      <ion-list>
        <ion-item v-for="c in clientes_store.clientes" :key="c.id">
          <ion-label>
            <h3>{{ c.razon_social }}</h3>
            <p>CUIT: {{ c.cuit }} · {{ c.localidad || 'Sin localidad' }}</p>
            <p v-if="c.telefono">📞 {{ c.telefono }}</p>
            <p v-if="c.email">✉️ {{ c.email }}</p>
          </ion-label>
        </ion-item>
      </ion-list>
    </template>
  </comp-page>
</template>

<script setup>
import { onMounted } from 'vue';
import { IonList, IonItem, IonLabel } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import { clientes_store } from '@/stores/clientes_store';

onMounted(() => {
  clientes_store.cargar();
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