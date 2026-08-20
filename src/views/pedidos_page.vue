<template>
  <comp-page titulo="Pedidos" :mostrar_actualizar="true" @actualizar="cargar">
    <comp-esqueleto v-if="cargando" />
    <ion-list v-else>
      <ion-item v-for="p in pedidos" :key="p.id">
        <ion-label>
          <h3>{{ p.id }} - {{ p.cliente }}</h3>
          <p>${{ p.total }} · Pago: {{ p.pago }}</p>
        </ion-label>
        <ion-badge slot="end" :color="p.color">{{ p.estado }}</ion-badge>
      </ion-item>
    </ion-list>
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { IonList, IonItem, IonLabel, IonBadge } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import { obtener_pedidos } from '../datos/pedidos';

const cargando = ref(true);
const pedidos = ref([]);

const cargar = async (event = null) => {
  cargando.value = true;
  const res = await obtener_pedidos();
  pedidos.value = res.pedidos;
  cargando.value = false;
  if (event) event.target.complete();
};

onMounted(() => cargar());
</script>