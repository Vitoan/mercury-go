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
        <ion-card-title class="mercury-titulo-destacado">Versión 5 · Autenticación, Roles & Biometría</ion-card-title>
      </ion-card-header>
      <ion-card-content>
        Capa de seguridad empresarial y control de acceso activa:
        <ul style="margin: 8px 0 0 16px; padding: 0;">
          <li><strong>Autenticación JWT & Refresh Tokens:</strong> Tokens en memoria con renovación transparente y rotación hasheada (SHA-256).</li>
          <li><strong>Roles en Base de Datos (B2B):</strong> Permisos dinámicos para Administrador, Operario, Chofer y Cliente.</li>
          <li><strong>Revocación Inmediata:</strong> Validación en cada petición (<code>OnTokenValidated</code>) ante bajas o cambios de permisos.</li>
          <li><strong>Seguridad Móvil & Biometría:</strong> Almacenamiento cifrado en Keystore y desbloqueo local por huella digital.</li>
        </ul>
      </ion-card-content>
    </ion-card>
  </comp-page>
</template>

<script setup>
import { IonCard, IonCardHeader, IonCardTitle, IonCardContent } from '@ionic/vue';
import CompPage from '../components/estructura/comp_page.vue';
</script>