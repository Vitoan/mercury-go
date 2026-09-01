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
        <ion-card-title>Versión 3 · Backend & MySQL</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        El estado real vive en el servidor. Catálogo, clientes y pedidos servidos por <strong>ASP.NET Core Web API</strong> conectada a <strong>MySQL</strong> con Entity Framework Core.
      </ion-card-content>
    </ion-card>
  </comp-page>
</template>

<script setup>
import { IonCard, IonCardHeader, IonCardTitle, IonCardContent } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
</script>