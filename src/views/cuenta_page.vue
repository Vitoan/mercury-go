<template>
  <comp-page titulo="Mi cuenta">
    <ion-list>
      <ion-item>
        <ion-label>
          <h3>Victor Angel Aguilera Ocampo</h3>
          <p>Rol</p>
        </ion-label>
      </ion-item>
      <ion-item>
        <ion-label>Modo Oscuro</ion-label>
        <ion-toggle :checked="modoOscuro" @ionChange="toggleTema"></ion-toggle>
      </ion-item>
    </ion-list>
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { IonList, IonItem, IonLabel, IonToggle } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
import { alternar_tema, es_tema_oscuro } from '../config/tema';

const modoOscuro = ref(false);

const toggleTema = () => {
  modoOscuro.value = alternar_tema();
};

onMounted(() => {
  modoOscuro.value = es_tema_oscuro();
});
</script>