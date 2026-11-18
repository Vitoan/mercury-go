<template>
  <div class="contenedor-estado ion-padding ion-text-center">
    <ion-icon :icon="fileTrayOutline" class="icono-estado color-vacio"></ion-icon>
    <h3 class="titulo-estado">No hay elementos</h3>
    <p class="mensaje-estado">{{ mensaje || 'No se encontraron registros disponibles para mostrar.' }}</p>
  </div>
</template>

<script setup>
import { IonIcon } from '@ionic/vue';
import { fileTrayOutline } from 'ionicons/icons';

defineProps({
  mensaje: {
    type: String,
    default: ''
  }
});
</script>

<style scoped>
.contenedor-estado {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 240px;
  margin-top: 24px;
}

.icono-estado {
  font-size: 64px;
  margin-bottom: 12px;
}

.color-vacio {
  color: var(--mercury-text-muted);
}

.titulo-estado {
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--mercury-text-title);
  margin: 4px 0 8px 0;
}

.mensaje-estado {
  color: var(--mercury-text-muted);
  max-width: 100%;
  font-size: 0.95rem;
}
</style>
