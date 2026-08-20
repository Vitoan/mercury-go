<template>
  <ion-page>
    <ion-header>
      <ion-toolbar>
        <ion-buttons slot="start">
          <ion-menu-button></ion-menu-button>
        </ion-buttons>
        <ion-title>{{ titulo }}</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content :fullscreen="true" class="ion-padding">
      <ion-refresher v-if="mostrar_actualizar" slot="fixed" @ionRefresh="ejecutar_actualizar">
        <ion-refresher-content></ion-refresher-content>
      </ion-refresher>
      <slot />
    </ion-content>
  </ion-page>
</template>

<script setup>
import { 
  IonPage, IonHeader, IonToolbar, IonTitle, IonButtons, 
  IonMenuButton, IonContent, IonRefresher, IonRefresherContent 
} from '@ionic/vue';

defineProps({
  titulo: { type: String, required: true },
  mostrar_actualizar: { type: Boolean, default: false }
});

const emit = defineEmits(['actualizar']);

const ejecutar_actualizar = (event) => {
  emit('actualizar', event);
};
</script>
