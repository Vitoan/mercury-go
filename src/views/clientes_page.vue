<template>
  <comp-page titulo="Clientes" :mostrar_actualizar="true" @actualizar="cargar">
    <comp-esqueleto v-if="cargando" />
    <ion-list v-else>
      <ion-item v-for="c in clientes" :key="c.id">
        <ion-label>
          <h3>{{ c.nombre }}</h3>
          <p>{{ c.telefono }}</p>
          <p>{{ c.direccion }}</p>
        </ion-label>
      </ion-item>
    </ion-list>
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { IonList, IonItem, IonLabel } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import { obtener_clientes } from '../datos/clientes';

const cargando = ref(true);
const clientes = ref([]);

const cargar = async (event = null) => {
  cargando.value = true;
  const res = await obtener_clientes();
  clientes.value = res.clientes;
  cargando.value = false;
  if (event) event.target.complete();
};

onMounted(() => cargar());
</script>