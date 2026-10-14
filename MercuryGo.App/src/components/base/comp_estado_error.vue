<template>
  <div class="contenedor-estado ion-padding ion-text-center">
    <ion-icon :icon="cloudOfflineOutline" class="icono-estado color-error"></ion-icon>
    <h3 class="titulo-estado">No se pudo cargar la información</h3>
    <p class="mensaje-estado">{{ mensaje || 'Error al conectar con la API del servidor.' }}</p>
    <ion-button color="primary" @click="$emit('reintentar')">
      <ion-icon slot="start" :icon="refreshOutline"></ion-icon>
      Reintentar
    </ion-button>
  </div>
</template>

<script setup>
import { IonIcon, IonButton } from '@ionic/vue';
import { cloudOfflineOutline, refreshOutline } from 'ionicons/icons';

defineProps({
  mensaje: {
    type: String,
    default: ''
  }
});

defineEmits(['reintentar']);
</script>

<style scoped>
.contenedor-estado {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 260px;
  margin-top: 24px;
}

.icono-estado {
  font-size: 64px;
  margin-bottom: 12px;
}

.color-error {
  color: var(--ion-color-danger, #ef4444);
}

.titulo-estado {
  font-size: 1.15rem;
  font-weight: 600;
  margin: 4px 0 8px 0;
}

.mensaje-estado {
  color: var(--ion-color-step-600, #64748b);
  max-width: 320px;
  font-size: 0.95rem;
  margin-bottom: 20px;
}
</style>
