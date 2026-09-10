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
import { sesion_store } from '@/stores/sesion_store';

const tabs = computed(() => {
  const rol = sesion_store.rol_activo.value;
  return navegacion
    .filter(i => {
      if (!i.en_tabs) return false;
      // Si está pendiente de habilitación (sin rol), solo ve inicio y cuenta
      if (!rol) return i.id === 'inicio' || i.id === 'cuenta';
      return !i.roles || i.roles.includes(rol);
    })
    .sort((a, b) => a.orden - b.orden);
});
</script>