<template>
  <ion-app>
    <ion-router-outlet />
  </ion-app>
</template>

<script setup>
import { onMounted } from 'vue';
import {
  IonApp,
  IonRouterOutlet,
  useIonRouter,
  modalController,
  menuController,
  alertController,
  actionSheetController
} from '@ionic/vue';
import { App as CapacitorApp } from '@capacitor/app';
import { Capacitor } from '@capacitor/core';

const ionRouter = useIonRouter();

onMounted(() => {
  if (!Capacitor.isNativePlatform()) return;

  // Manejo del botón físico o gesto nativo Atrás de Android (AUDIT-M02-01)
  CapacitorApp.addListener('backButton', async () => {
    // 1. Cerrar cualquier overlay abierto antes de navegar
    const modal = await modalController.getTop();
    if (modal) {
      await modal.dismiss();
      return;
    }

    const actionSheet = await actionSheetController.getTop();
    if (actionSheet) {
      await actionSheet.dismiss();
      return;
    }

    const alert = await alertController.getTop();
    if (alert) {
      await alert.dismiss();
      return;
    }

    const menuAbierto = await menuController.isOpen();
    if (menuAbierto) {
      await menuController.close();
      return;
    }

    // 2. Si hay historial de navegación en la app, retroceder una pantalla
    if (ionRouter.canGoBack()) {
      ionRouter.back();
    } else {
      // 3. En pantallas raíz, minimizar la aplicación sin forzar cierre abrupto
      CapacitorApp.exitApp();
    }
  });
});
</script>