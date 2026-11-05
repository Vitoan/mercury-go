<template>
  <div class="mercury-contenedor-lista">
    <ion-list class="mercury-lista-items" lines="none">
      <slot />
    </ion-list>

    <!-- Scroll infinito normalizado (Semana 9):
         Sensor al pie de lista que solicita la siguiente página al alcanzar el umbral.
         Se desactiva automáticamente si no hay más registros (:disabled="!hay_mas")
         para evitar spinners infinitos o consultas innecesarias a la red. -->
    <ion-infinite-scroll
      threshold="120px"
      :disabled="!hay_mas"
      @ionInfinite="al_scrollear"
    >
      <div class="mercury-cargando-mas">
        <template v-if="pidiendo || cargando">
          <ion-spinner name="crescent" />
          <span>Cargando más...</span>
        </template>
      </div>
    </ion-infinite-scroll>

    <p v-if="total > 0" class="mercury-pie-conteo">
      Mostrando <strong>{{ mostrados }}</strong> de <strong>{{ total }}</strong> registros
    </p>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { IonList, IonInfiniteScroll, IonSpinner } from '@ionic/vue';

// Componente base genérico para listas paginadas normalizadas:
// Maneja el sensor de scroll infinito y notifica al contenedor para cargar más.
const props = defineProps({
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
  },
  cargar_mas: {
    type: Function,
    default: null
  }
});

const emit = defineEmits(['cargar_mas']);
const pidiendo = ref(false);

const al_scrollear = async (evento) => {
  pidiendo.value = true;
  try {
    if (typeof props.cargar_mas === 'function') {
      await props.cargar_mas();
    } else {
      emit('cargar_mas');
    }
  } catch (error) {
    console.error('Error al cargar más registros:', error);
  } finally {
    pidiendo.value = false;
    if (evento?.target?.complete) {
      evento.target.complete();
    }
  }
};
</script>

<style scoped>
.mercury-contenedor-lista {
  width: 100%;
}

.mercury-lista-items {
  background: transparent;
  padding: 0;
}

.mercury-cargando-mas {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 14px 0;
  color: var(--ion-color-step-600, #94a3b8);
  font-size: 0.85rem;
  font-weight: 500;
  min-height: 48px;
}

.mercury-pie-conteo {
  text-align: center;
  font-size: 0.8rem;
  color: var(--ion-color-step-600, #94a3b8);
  margin-top: 6px;
  margin-bottom: 24px;
}
</style>
