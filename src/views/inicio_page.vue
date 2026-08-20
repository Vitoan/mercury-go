<template>
  <comp-page titulo="MercuryGO">
    <ion-card style="margin-inline: 0;">
      <ion-card-header>
        <ion-card-title style="color: #38BDF8;">Logística en Movimiento</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Bienvenido a MercuryGO. Sistema integral para gestión de pedidos, rutas de carga LIFO y control de stock.
      </ion-card-content>
    </ion-card>

    <ion-card style="margin-inline: 0; margin-top: 16px;">
      <ion-card-header>
        <ion-card-title>Versión 2</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Las rutas, el menú lateral y los tabs salen todos del mismo archivo: <code>src/config/navegacion.js</code>.
      </ion-card-content>
    </ion-card>
  </comp-page>
</template>

<script setup>
import { IonCard, IonCardHeader, IonCardTitle, IonCardContent } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
</script>