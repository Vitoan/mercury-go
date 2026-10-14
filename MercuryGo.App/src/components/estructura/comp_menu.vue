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

      <template v-if="items_administracion.length > 0">
        <ion-list-header>Administración</ion-list-header>
        <ion-list>
          <ion-menu-toggle :auto-hide="false" v-for="item in items_administracion" :key="item.id">
            <ion-item :router-link="item.ruta" router-direction="root" lines="none">
              <ion-icon slot="start" :icon="item.icono"></ion-icon>
              <ion-label>{{ item.titulo }}</ion-label>
            </ion-item>
          </ion-menu-toggle>
        </ion-list>
      </template>

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
import { sesion_store } from '@/stores/sesion_store';

defineProps({
  content_id: { type: String, required: true }
});

const puede_ver = (item) => {
  const rol = sesion_store.rol_activo;
  if (!rol) return item.id === 'inicio' || item.id === 'cuenta';
  return !item.roles || item.roles.includes(rol);
};

const items_principales = computed(() => 
  navegacion
    .filter(i => i.grupo_menu === 'principal' && puede_ver(i))
    .sort((a, b) => a.orden - b.orden)
);

const items_administracion = computed(() => 
  navegacion
    .filter(i => i.grupo_menu === 'administracion' && puede_ver(i))
    .sort((a, b) => a.orden - b.orden)
);

const items_configuracion = computed(() => 
  navegacion
    .filter(i => i.grupo_menu === 'configuracion' && puede_ver(i))
    .sort((a, b) => a.orden - b.orden)
);
</script>