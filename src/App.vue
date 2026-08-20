<script setup>
import { ref, onMounted } from 'vue';
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
import { moonOutline, sunnyOutline } from 'ionicons/icons';

// Estado reactivo para rastrear si el modo oscuro está activo
const esOscuro = ref(false);

// Listado reactivo de módulos funcionales de MercuryGO
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

// Función para alternar el tema claro/oscuro y persistir en localStorage
const cambiarTema = () => {
  const html = document.documentElement;
  esOscuro.value = html.classList.toggle('ion-palette-dark');
  localStorage.setItem('mi_app_tema', esOscuro.value ? 'oscuro' : 'claro');
};

// Al montar la vista, sincroniza el icono con el estado actual del DOM
onMounted(() => {
  esOscuro.value = document.documentElement.classList.contains('ion-palette-dark');
});
</script>

<template>
  <ion-page>
    <!-- Encabezado superior con nombre de alumno y botón de tema -->
    <ion-header>
      <ion-toolbar>
        <ion-title>Victor Angel Aguilera Ocampo</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cambiarTema">
            <ion-icon slot="icon-only" :icon="esOscuro ? sunnyOutline : moonOutline"></ion-icon>
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <!-- Contenido principal scrollable -->
    <ion-content class="ion-padding">
      <ion-title size="large" class="ion-no-padding">MercuryGO</ion-title>
      <p style="color: var(--ion-color-step-600); margin-top: 4px;">
        Logística y Distribución en Movimiento
      </p>

      <!-- Tarjetas renderizadas dinámicamente -->
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