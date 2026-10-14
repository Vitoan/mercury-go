<template>
  <div class="mercury-contenedor-lista">
    <ion-list class="mercury-lista-items">
      <slot />
    </ion-list>

    <!-- El botón 'Ver más' solo aparece si la API confirma que hay_mas.
         No se deduce de la cantidad descargada (evita falsos positivos). -->
    <div v-if="hay_mas" class="mercury-acciones-paginacion ion-padding">
      <ion-button
        expand="block"
        fill="outline"
        size="default"
        class="mercury-boton-ver-mas"
        :disabled="cargando"
        @click="$emit('cargar_mas')"
      >
        <ion-spinner v-if="cargando" name="crescent" />
        <span v-else>Cargar más registros</span>
      </ion-button>
    </div>

    <p v-if="total > 0" class="mercury-pie-conteo">
      Mostrando <strong>{{ mostrados }}</strong> de <strong>{{ total }}</strong> registros
    </p>
  </div>
</template>

<script setup>
import { IonList, IonButton, IonSpinner } from '@ionic/vue';

// Componente base genérico para listas paginadas:
// Recibe si hay más páginas y avisa cuándo pedirlas. No conoce la entidad ni cómo se pide a la red.
defineProps({
  cargando: {
    type: Boolean,
    default: false
  },
  hay_mas: {
    type: Boolean,
    default: false
  },
  mostrados: {
    type: Number,
    default: 0
  },
  total: {
    type: Number,
    default: 0
  }
});

defineEmits(['cargar_mas']);
</script>

<style scoped>
.mercury-contenedor-lista {
  width: 100%;
}

.mercury-lista-items {
  background: transparent;
  padding: 0;
}

.mercury-acciones-paginacion {
  padding-top: 12px;
  padding-bottom: 4px;
}

.mercury-boton-ver-mas {
  --border-radius: 8px;
  font-weight: 600;
  text-transform: none;
  letter-spacing: 0.3px;
}

.mercury-pie-conteo {
  text-align: center;
  font-size: 0.8rem;
  color: var(--ion-color-step-600, #94a3b8);
  margin-top: 6px;
  margin-bottom: 20px;
}
</style>
