<template>
  <ion-page>
    <comp-menu content_id="contenido_principal" />
    <ion-tabs id="contenido_principal">
      <ion-router-outlet />
      <comp-tabs />
    </ion-tabs>
  </ion-page>
</template>

<script setup>
import { IonPage, IonTabs, IonRouterOutlet } from '@ionic/vue';
import CompMenu from '../components/estructura/comp_menu.vue';
import CompTabs from '../components/estructura/comp_tabs.vue';
</script>