<script setup>
import { ref } from 'vue';
import { 
  IonPage, 
  IonHeader, 
  IonToolbar, 
  IonTitle, 
  IonButtons, 
  IonButton, 
  IonIcon, 
  IonContent, 
  IonCard, 
  IonCardHeader, 
  IonCardTitle, 
  IonCardContent 
} from '@ionic/vue';
import { moonOutline } from 'ionicons/icons';

// Array reactivo con el detalle funcional de MercuryGO
const servicios = ref([
  {
    titulo: 'Portal de Pedidos y Carrito',
    detalle: 'Catálogo online con precios por cliente, armado de carrito de compras y envío de comprobantes por WhatsApp.'
  },
  {
    titulo: 'Gestión de Depósito y Hoja de Ruta',
    detalle: 'Preparación de bultos, orden de carga LIFO (último a descargar, primero a cargar) y control de stock.'
  },
  {
    titulo: 'Cuentas Corrientes y Facturación',
    detalle: 'Registro de deuda, validación de comprobantes de pago (transferencias/efectivo) y facturación tras la carga.'
  }
]);

// Función para alternar el tema claro/oscuro y guardar la elección en localStorage
const cambiarTema = () => {
  const html = document.documentElement;
  const oscuro = html.classList.toggle('ion-palette-dark');
  localStorage.setItem('mi_app_tema', oscuro ? 'oscuro' : 'claro');
};
</script>

<template>
  <ion-page>
    <ion-header>
      <ion-toolbar>
        <!-- Tu nombre en el encabezado superior (Requisito obligatorio de la entrega) -->
        <ion-title>Victor Angel Aguilera Ocampo</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cambiarTema">
            <ion-icon slot="icon-only" :icon="moonOutline"></ion-icon>
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <!-- Identidad y Título de tu aplicación -->
      <ion-title size="large" class="ion-no-padding">MercuryGO</ion-title>
      <p style="color: var(--ion-color-step-600); margin-top: 4px;">
        Logística y Distribución en Movimiento
      </p>

      <!-- Tarjetas renderizadas dinámicamente con v-for -->
      <ion-card 
        v-for="item in servicios" 
        :key="item.titulo" 
        style="margin-inline: 0; margin-top: 16px;"
      >
        <ion-card-header>
          <ion-card-title style="color: #38BDF8;">{{ item.titulo }}</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          {{ item.detalle }}
        </ion-card-content>
      </ion-card>
    </ion-content>
  </ion-page>
</template>