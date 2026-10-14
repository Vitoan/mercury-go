<template>
  <ion-searchbar
    class="mercury-buscador"
    :value="modelValue"
    :placeholder="placeholder"
    :debounce="0"
    @ionInput="alEscribir"
  />
</template>

<script setup>
import { onBeforeUnmount } from 'vue';
import { IonSearchbar } from '@ionic/vue';

// Espera a que el operador o preventista deje de tipear antes de consultar a la API.
// Sin esto, escribir "ARROZ" dispara 5 consultas y las respuestas pueden llegar desordenadas
// por latencia de red, dejando en pantalla resultados inconsistentes.
const props = defineProps({
  espera: {
    type: Number,
    default: 400
  },
  modelValue: {
    type: String,
    default: ''
  },
  placeholder: {
    type: String,
    default: 'Buscar por código, remito o nombre…'
  }
});

const emit = defineEmits(['update:modelValue', 'buscar']);

let temporizador = null;

const alEscribir = (evento) => {
  const texto = evento.detail.value || '';
  emit('update:modelValue', texto);

  if (temporizador) clearTimeout(temporizador);
  temporizador = setTimeout(() => {
    emit('buscar', texto.trim());
  }, props.espera);
};

onBeforeUnmount(() => {
  if (temporizador) clearTimeout(temporizador);
});
</script>

<style scoped>
.mercury-buscador {
  padding-inline: 12px;
  padding-top: 4px;
  padding-bottom: 8px;
  --box-shadow: none;
  --background: var(--ion-color-step-100, rgba(255, 255, 255, 0.05));
  --border-radius: 10px;
}
</style>
