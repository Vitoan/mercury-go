<template>
  <comp-page titulo="Catálogo de Mercadería" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Buscador con debounce en servidor -->
    <comp-buscador
      v-model="busqueda"
      placeholder="Buscar mercadería, bulto o marca…"
      @buscar="alBuscar"
    />

    <!-- Fila de Chips de Categorías con conteo real desde la base de datos -->
    <div class="mercury-seccion-chips">
      <div class="mercury-chips-scroll">
        <ion-chip
          :color="categoriaSeleccionada === null ? 'primary' : 'medium'"
          :outline="categoriaSeleccionada !== null"
          class="mercury-chip"
          @click="filtrarCategoria(null)"
        >
          <ion-icon :icon="gridOutline" />
          <ion-label>Todas · {{ productos_store.resumen.total }}</ion-label>
        </ion-chip>

        <ion-chip
          v-for="cat in productos_store.categorias"
          :key="cat.id"
          :color="categoriaSeleccionada === cat.id ? 'primary' : 'medium'"
          :outline="categoriaSeleccionada !== cat.id"
          class="mercury-chip"
          @click="filtrarCategoria(cat.id)"
        >
          <ion-icon :icon="cubeOutline" />
          <ion-label>{{ cat.nombre }} · {{ cat.cantidad }}</ion-label>
        </ion-chip>
      </div>
    </div>

    <!-- Chips de Disponibilidad de Stock -->
    <div class="mercury-seccion-chips mercury-chips-secundarios">
      <div class="mercury-chips-scroll">
        <ion-chip
          :color="filtroDisponible === null ? 'primary' : 'medium'"
          :outline="filtroDisponible !== null"
          class="mercury-chip mercury-chip-sm"
          @click="filtrarDisponibilidad(null)"
        >
          <ion-label>Todo el Stock</ion-label>
        </ion-chip>

        <ion-chip
          :color="filtroDisponible === true ? 'success' : 'medium'"
          :outline="filtroDisponible !== true"
          class="mercury-chip mercury-chip-sm"
          @click="filtrarDisponibilidad(true)"
        >
          <ion-icon :icon="checkmarkCircleOutline" />
          <ion-label>En Stock · {{ productos_store.resumen.disponibles }}</ion-label>
        </ion-chip>

        <ion-chip
          :color="filtroDisponible === false ? 'danger' : 'medium'"
          :outline="filtroDisponible !== false"
          class="mercury-chip mercury-chip-sm"
          @click="filtrarDisponibilidad(false)"
        >
          <ion-icon :icon="closeCircleOutline" />
          <ion-label>Sin Stock · {{ productos_store.resumen.no_disponibles }}</ion-label>
        </ion-chip>
      </div>
    </div>

    <!-- Estado 1: Cargando primera página -->
    <comp-esqueleto v-if="productos_store.cargando && !productos_store.hay_productos" />

    <!-- Estado 2: Error al conectar con la API -->
    <comp-estado-error
      v-else-if="productos_store.error && !productos_store.hay_productos"
      :mensaje="productos_store.error"
      @reintentar="recargar"
    />

    <!-- Estado 3: Respuesta exitosa pero vacía -->
    <comp-estado-vacio
      v-else-if="!productos_store.hay_productos"
      mensaje="No se encontraron productos con los filtros aplicados."
    />

    <!-- Estado 4: Lista paginada desde el servidor -->
    <comp-lista
      v-else
      :cargando="productos_store.cargando"
      :hay_mas="productos_store.hay_mas"
      :mostrados="productos_store.mostrados"
      :total="productos_store.total"
      @cargar_mas="productos_store.cargar_mas"
    >
      <ion-item v-for="p in productos_store.productos" :key="p.id" class="mercury-producto-item">
        <ion-icon :icon="cubeOutline" slot="start" class="mercury-icono-producto" />
        <ion-label>
          <div class="mercury-producto-fila-titulo">
            <h3>{{ p.nombre }}</h3>
            <span class="mercury-producto-precio">${{ p.precio.toLocaleString('es-AR') }}</span>
          </div>
          <p class="mercury-producto-categoria">{{ p.categoria_nombre }} · {{ p.descripcion || 'Sin descripción' }}</p>
        </ion-label>
        <ion-badge
          slot="end"
          :color="p.disponible ? 'success' : 'medium'"
          class="mercury-badge-stock"
        >
          {{ p.disponible ? 'En Stock' : 'Agotado' }}
        </ion-badge>
      </ion-item>
    </comp-lista>
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import {
  IonItem,
  IonLabel,
  IonBadge,
  IonChip,
  IonIcon
} from '@ionic/vue';
import {
  gridOutline,
  cubeOutline,
  checkmarkCircleOutline,
  closeCircleOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';

import { productos_store } from '@/stores/productos_store';

const busqueda = ref('');
const categoriaSeleccionada = ref(null);
const filtroDisponible = ref(null);

const alBuscar = (texto) => {
  productos_store.establecer_busqueda(texto);
};

const filtrarCategoria = (catId) => {
  categoriaSeleccionada.value = catId;
  productos_store.establecer_categoria(catId);
};

const filtrarDisponibilidad = (valor) => {
  filtroDisponible.value = valor;
  productos_store.establecer_disponible(valor);
};

const recargar = (event = null) => {
  productos_store.cargar_resumen();
  productos_store.cargar_listado(true, event);
};

onMounted(async () => {
  await productos_store.cargar_resumen();
  await productos_store.cargar_listado(true);
});
</script>

<style scoped>
.mercury-seccion-chips {
  padding-bottom: 2px;
}

.mercury-chips-secundarios {
  padding-bottom: 8px;
}

.mercury-chips-scroll {
  display: flex;
  overflow-x: auto;
  padding: 0 12px;
  gap: 6px;
  scrollbar-width: none;
  -webkit-overflow-scrolling: touch;
}

.mercury-chips-scroll::-webkit-scrollbar {
  display: none;
}

.mercury-chip {
  font-size: 0.82rem;
  font-weight: 600;
  margin: 0;
  flex-shrink: 0;
  --border-radius: 8px;
  cursor: pointer;
}

.mercury-chip-sm {
  font-size: 0.76rem;
  height: 28px;
}

.mercury-producto-item {
  --padding-start: 12px;
  --padding-end: 12px;
  --padding-top: 8px;
  --padding-bottom: 8px;
  --border-color: var(--ion-color-step-150, rgba(255, 255, 255, 0.08));
}

.mercury-icono-producto {
  font-size: 1.5rem;
  color: var(--ion-color-primary, #38bdf8);
}

.mercury-producto-fila-titulo {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.mercury-producto-fila-titulo h3 {
  font-weight: 600;
  font-size: 0.98rem;
  margin: 0;
  color: var(--ion-text-color, #f8fafc);
}

.mercury-producto-precio {
  font-weight: 700;
  font-size: 1.05rem;
  color: #38bdf8;
}

.mercury-producto-categoria {
  font-size: 0.82rem;
  color: var(--ion-color-step-600, #94a3b8);
  margin-top: 2px;
}

.mercury-badge-stock {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 4px 8px;
  border-radius: 6px;
}
</style>