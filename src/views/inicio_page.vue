<template>
  <comp-page titulo="Ofertas MercuryGO" :mostrar_actualizar="true" @actualizar="alActualizar">
    <template #acciones>
      <ion-button v-if="sesion_store.es_admin" title="Agregar Nuevo Producto" @click="abrirNuevoProducto">
        <ion-icon slot="icon-only" :icon="addCircleOutline" />
      </ion-button>
    </template>

    <div class="mercury-inicio-ofertas-contenedor">
      <div class="mercury-ofertas-encabezado">
        <div class="mercury-ofertas-titulo">
          <ion-icon :icon="flameOutline" color="danger" />
          <h2>🔥 Ofertas Especiales y Descuentos</h2>
        </div>
        <p class="mercury-ofertas-subtitulo">Precios exclusivos de distribución mayorista. Deslizá hacia abajo para ver todas las promociones.</p>
      </div>

      <!-- Estado: Sin ofertas activas -->
      <div v-if="!productos_store.cargando && ofertas.length === 0" class="mercury-sin-ofertas">
        <ion-icon :icon="flameOutline" class="mercury-icono-vacio" />
        <h3>No hay ofertas activas en este momento</h3>
        <p>Volvé a consultar pronto para ver nuevos precios especiales de distribución.</p>
        <ion-button
          v-if="sesion_store.es_admin"
          fill="outline"
          color="primary"
          style="margin-top: 14px;"
          @click="abrirNuevoProducto"
        >
          <ion-icon slot="start" :icon="addCircleOutline" />
          Publicar Producto en Oferta
        </ion-button>
      </div>

      <!-- Lista vertical deslizable hacia abajo de ofertas -->
      <div v-else class="mercury-ofertas-lista-vertical">
        <ion-card v-for="oferta in ofertas" :key="oferta.id" class="mercury-card-oferta-vertical">
          <div class="mercury-oferta-img-bloque">
            <img
              v-if="oferta.imagen_url"
              :src="resolver_url_imagen(oferta.imagen_url)"
              :alt="oferta.nombre"
              class="mercury-oferta-foto"
              @error="oferta.imagen_url = null"
            />
            <div v-else class="mercury-oferta-sin-foto">
              <ion-icon :icon="cubeOutline" />
            </div>

            <ion-badge color="danger" class="mercury-badge-oferta-flotante">
              🔥 OFERTA
            </ion-badge>

            <!-- Botón de edición rápida en la tarjeta para el ADMIN -->
            <ion-button
              v-if="sesion_store.es_admin"
              size="small"
              fill="solid"
              color="light"
              class="mercury-btn-editar-oferta"
              title="Editar Oferta"
              @click="abrirEditarProducto(oferta)"
            >
              <ion-icon slot="icon-only" :icon="createOutline" />
            </ion-button>
          </div>

          <ion-card-content class="mercury-oferta-datos">
            <h3 class="mercury-oferta-titulo">{{ oferta.nombre }}</h3>
            <p class="mercury-oferta-cat">{{ oferta.categoria_nombre }}</p>
            <p class="mercury-oferta-desc">{{ oferta.descripcion || 'Precio promocional por tiempo limitado.' }}</p>

            <div class="mercury-oferta-acciones-pie">
              <span class="mercury-oferta-precio-grande">${{ oferta.precio?.toLocaleString('es-AR') }}</span>
              <ion-button
                size="small"
                color="primary"
                class="mercury-btn-sumar"
                @click="sumarAlCarrito(oferta)"
              >
                <ion-icon slot="start" :icon="cartOutline" />
                Agregar al Carrito
              </ion-button>
            </div>
          </ion-card-content>
        </ion-card>
      </div>
    </div>

    <!-- Modal para Agregar o Editar Producto (Admin) -->
    <comp-producto-modal
      :abierto="mostrarModalProducto"
      :producto="productoSeleccionadoParaEditar"
      @cerrar="mostrarModalProducto = false"
      @producto_guardado="alGuardarProducto"
    />
  </comp-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import {
  IonCard,
  IonCardContent,
  IonBadge,
  IonButton,
  IonIcon
} from '@ionic/vue';
import {
  flameOutline,
  cubeOutline,
  cartOutline,
  createOutline,
  addCircleOutline
} from 'ionicons/icons';
import CompPage from '../components/estructura/comp_page.vue';
import CompProductoModal from '../components/dominio/productos/comp_producto_modal.vue';
import { productos_store } from '@/stores/productos_store';
import { carrito_store } from '@/stores/carrito_store';
import { sesion_store } from '@/stores/sesion_store';
import { resolver_url_imagen } from '@/utils/imagenes';
import { vibrar_toque, vibrar_exito } from '@/services/vibracion_service';

const mostrarModalProducto = ref(false);
const productoSeleccionadoParaEditar = ref(null);

const ofertas = computed(() => productos_store.ofertas);

const sumarAlCarrito = (producto) => {
  vibrar_toque();
  carrito_store.agregar_producto(producto, 1);
};

const abrirNuevoProducto = () => {
  vibrar_toque();
  productoSeleccionadoParaEditar.value = null;
  mostrarModalProducto.value = true;
};

const abrirEditarProducto = (producto) => {
  vibrar_toque();
  productoSeleccionadoParaEditar.value = producto;
  mostrarModalProducto.value = true;
};

const alGuardarProducto = async () => {
  await vibrar_exito();
  await productos_store.cargar_resumen();
};

const alActualizar = async (event) => {
  await productos_store.cargar_resumen(event);
};

onMounted(() => {
  productos_store.cargar_resumen();
});
</script>

<style scoped>
.mercury-inicio-ofertas-contenedor {
  padding-bottom: 24px;
}

.mercury-ofertas-encabezado {
  margin-bottom: 14px;
}

.mercury-ofertas-titulo {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mercury-ofertas-titulo h2 {
  font-size: 1.15rem;
  font-weight: 800;
  margin: 0;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-ofertas-subtitulo {
  font-size: 0.85rem;
  color: var(--ion-color-medium, #64748b);
  margin: 4px 0 0 0;
  line-height: 1.35;
}

.mercury-sin-ofertas {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 48px 16px;
  color: var(--ion-color-medium, #64748b);
}

.mercury-icono-vacio {
  font-size: 3.5rem;
  color: var(--ion-color-step-400, #cbd5e1);
  margin-bottom: 10px;
}

/* Lista vertical deslizable hacia abajo */
.mercury-ofertas-lista-vertical {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.mercury-card-oferta-vertical {
  margin: 0;
  border-radius: 14px;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.08);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.mercury-oferta-img-bloque {
  position: relative;
  width: 100%;
  height: 170px;
  background: var(--ion-color-light, #f1f5f9);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.mercury-oferta-foto {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.mercury-oferta-sin-foto {
  font-size: 3.8rem;
  color: var(--ion-color-medium, #94a3b8);
}

.mercury-badge-oferta-flotante {
  position: absolute;
  top: 10px;
  left: 10px;
  font-size: 0.74rem;
  font-weight: 800;
  padding: 4px 8px;
  border-radius: 6px;
}

.mercury-btn-editar-oferta {
  position: absolute;
  top: 8px;
  right: 8px;
  --border-radius: 50%;
  width: 34px;
  height: 34px;
  opacity: 0.95;
}

.mercury-oferta-datos {
  padding: 14px;
  display: flex;
  flex-direction: column;
}

.mercury-oferta-titulo {
  font-size: 1.08rem;
  font-weight: 700;
  margin: 0 0 2px 0;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-oferta-cat {
  font-size: 0.78rem;
  font-weight: 600;
  color: var(--ion-color-primary, #2563eb);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0 0 4px 0;
}

.mercury-oferta-desc {
  font-size: 0.85rem;
  color: var(--ion-color-medium, #64748b);
  margin: 0 0 14px 0;
  line-height: 1.35;
}

.mercury-oferta-acciones-pie {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-top: 1px solid var(--ion-color-light, #f1f5f9);
  padding-top: 10px;
}

.mercury-oferta-precio-grande {
  font-size: 1.35rem;
  font-weight: 800;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-btn-sumar {
  --border-radius: 8px;
  font-weight: 600;
}
</style>