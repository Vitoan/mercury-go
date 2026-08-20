<template>
  <ion-menu :content-id="content_id" type="overlay">
    <ion-header>
      <ion-toolbar>
        <ion-title>MercuryGO</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-list>
        <ion-menu-toggle :auto-hide="false" v-for="item in items_principales" :key="item.id">
          <ion-item :router-link="item.ruta" router-direction="root" lines="none">
            <ion-icon slot="start" :icon="item.icono"></ion-icon>
            <ion-label>{{ item.titulo }}</ion-label>
          </ion-item>
        </ion-menu-toggle>
      </ion-list>

      <ion-list-header>Configuración</ion-list-header>
      <ion-list>
        <ion-menu-toggle :auto-hide="false" v-for="item in items_configuracion" :key="item.id">
          <ion-item :router-link="item.ruta" router-direction="root" lines="none">
            <ion-icon slot="start" :icon="item.icono"></ion-icon>
            <ion-label>{{ item.titulo }}</ion-label>
          </ion-item>
        </ion-menu-toggle>
      </ion-list>
    </ion-content>
  </ion-menu>
</template>

<script setup>
import { computed } from 'vue';
import { 
  IonMenu, IonHeader, IonToolbar, IonTitle, IonContent, 
  IonList, IonListHeader, IonItem, IonIcon, IonLabel, IonMenuToggle 
} from '@ionic/vue';
import { navegacion } from '../../config/navegacion';

defineProps({
  content_id: { type: String, required: true }
});

const items_principales = computed(() => 
  navegacion
    .filter(i => i.grupo_menu === 'principal')
    .sort((a, b) => a.orden - b.orden)
);

const items_configuracion = computed(() => 
  navegacion
    .filter(i => i.grupo_menu === 'configuracion')
    .sort((a, b) => a.orden - b.orden)
);
</script>