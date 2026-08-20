<template>
  <comp-page titulo="Productos" :mostrar_actualizar="true" @actualizar="cargar">
    <comp-esqueleto v-if="cargando" />
    <ion-list v-else>
      <ion-item v-for="p in productos" :key="p.id">
        <ion-label>
          <h3>{{ p.nombre }}</h3>
          <p>{{ p.detalle }}</p>
          <p><strong>${{ p.precio }}</strong></p>
        </ion-label>
        <ion-badge slot="end" :color="p.stock ? 'success' : 'medium'">
          {{ p.stock ? 'Stock' : 'Sin stock' }}
        </ion-badge>
      </ion-item>
    </ion-list>
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { IonList, IonItem, IonLabel, IonBadge } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import { obtener_productos } from '../datos/productos';

const cargando = ref(true);
const productos = ref([]);

const cargar = async (event = null) => {
  cargando.value = true;
  const res = await obtener_productos();
  productos.value = res.productos;
  cargando.value = false;
  if (event) event.target.complete();
};

onMounted(() => cargar());
</script>