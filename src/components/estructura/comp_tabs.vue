<template>
  <ion-tab-bar slot="bottom">
    <ion-tab-button v-for="item in tabs" :key="item.id" :tab="item.id" :href="item.ruta">
      <ion-icon :icon="item.icono" />
      <ion-label>{{ item.titulo }}</ion-label>
    </ion-tab-button>
  </ion-tab-bar>
</template>

<script setup>
import { computed } from 'vue';
import { IonTabBar, IonTabButton, IonIcon, IonLabel } from '@ionic/vue';
import { navegacion } from '../../config/navegacion';

const tabs = computed(() => 
  navegacion
    .filter(i => i.en_tabs)
    .sort((a, b) => a.orden - b.orden)
);
</script>