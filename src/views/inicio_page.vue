<template>
  <comp-page titulo="MercuryGO">
    <ion-card style="margin-inline: 0;">
      <ion-card-header>
        <ion-card-title class="mercury-titulo-destacado">Logística en Movimiento</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Bienvenido a MercuryGO. Sistema integral para gestión de pedidos, rutas de carga LIFO y control de stock.
      </ion-card-content>
    </ion-card>

    <ion-card style="margin-inline: 0; margin-top: 16px;">
      <ion-card-header>
        <ion-card-title class="mercury-titulo-destacado">Versión 4 · Paginación, ABM & Estados</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Flujo central transaccional activo:
        <ul style="margin: 8px 0 0 16px; padding: 0;">
          <li><strong>Paginación en Servidor:</strong> Listas con <code>Skip</code>/<code>Take</code> y carga incremental.</li>
          <li><strong>Búsqueda con Debounce:</strong> Consultas optimizadas directo a MySQL.</li>
          <li><strong>ABM con Validación en API:</strong> Altas y ediciones en modal con baja lógica.</li>
          <li><strong>Gestión de Estados:</strong> Ciclo de vida para órdenes de carga y precios protegidos.</li>
        </ul>
      </ion-card-content>
    </ion-card>
  </comp-page>
</template>

<script setup>
import { IonCard, IonCardHeader, IonCardTitle, IonCardContent } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
</script>