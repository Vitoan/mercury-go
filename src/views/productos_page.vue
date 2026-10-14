<template>
  <comp-page titulo="Catálogo de Mercadería" :mostrar_actualizar="true" @actualizar="recargar">
    <!-- Botón del Carrito y Nuevo Producto en Toolbar -->
    <template #acciones>
      <ion-button v-if="sesion_store.es_admin" title="Nuevo Producto" @click="abrirNuevoProducto">
        <ion-icon slot="icon-only" :icon="addCircleOutline" />
      </ion-button>
      <ion-button class="mercury-btn-carrito" @click="mostrarCarrito = true">
        <ion-icon slot="icon-only" :icon="cartOutline" />
        <ion-badge v-if="carrito_store.cantidad_total > 0" color="danger" class="mercury-badge-carrito">
          {{ carrito_store.cantidad_total }}
        </ion-badge>
      </ion-button>
    </template>

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

    <!-- Chips de Disponibilidad y Ofertas -->
    <div class="mercury-seccion-chips mercury-chips-secundarios">
      <div class="mercury-chips-scroll">
        <ion-chip
          :color="filtroDisponible === null && filtroOferta === null ? 'primary' : 'medium'"
          :outline="filtroDisponible !== null || filtroOferta !== null"
          class="mercury-chip mercury-chip-sm"
          @click="limpiarFiltrosSecundarios"
        >
          <ion-label>Todo el Catálogo</ion-label>
        </ion-chip>

        <ion-chip
          :color="filtroOferta === true ? 'danger' : 'medium'"
          :outline="filtroOferta !== true"
          class="mercury-chip mercury-chip-sm"
          @click="toggleOferta"
        >
          <ion-icon :icon="flameOutline" />
          <ion-label>🔥 Ofertas</ion-label>
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

    <!-- Estado 4: Lista paginada con tarjeta destacada (Imagen arriba, datos abajo) -->
    <comp-lista
      v-else
      :cargando="productos_store.cargando"
      :hay_mas="productos_store.hay_mas"
      :mostrados="productos_store.mostrados"
      :total="productos_store.total"
      @cargar_mas="productos_store.cargar_mas"
    >
      <div class="mercury-productos-grid">
        <ion-card v-for="p in productos_store.productos" :key="p.id" class="mercury-card-producto">
          <!-- Bloque Superior: Imagen y Badges -->
          <div class="mercury-producto-img-bloque">
            <img
              v-if="p.imagen_url && !imagenesConError[p.id]"
              :key="obtenerSrcImagen(p)"
              :src="obtenerSrcImagen(p)"
              :alt="p.nombre"
              class="mercury-producto-foto-portada"
              @error="marcarErrorImagen(p)"
            />
            <div v-else class="mercury-producto-sin-foto">
              <ion-icon :icon="cubeOutline" />
            </div>

            <div class="mercury-badges-flotantes">
              <ion-badge v-if="p.en_oferta" color="danger" class="mercury-flotante-badge">
                🔥 OFERTA
              </ion-badge>
              <ion-badge :color="p.disponible ? 'success' : 'medium'" class="mercury-flotante-badge">
                {{ p.disponible ? 'En Stock' : 'Sin Stock' }}
              </ion-badge>
            </div>

            <div v-if="sesion_store.es_admin" class="mercury-acciones-admin-flotantes">
              <ion-button
                size="small"
                fill="solid"
                color="light"
                class="mercury-btn-accion-flotante"
                title="Editar Producto"
                @click="abrirEditarProducto(p)"
              >
                <ion-icon slot="icon-only" :icon="createOutline" />
              </ion-button>
              <ion-button
                size="small"
                fill="solid"
                color="light"
                class="mercury-btn-accion-flotante"
                title="Cambiar foto con cámara o galería"
                @click="abrirOpcionesFoto(p)"
              >
                <ion-icon slot="icon-only" :icon="cameraOutline" />
              </ion-button>
            </div>
          </div>

          <!-- Bloque Inferior: Nombre, Descripción y Pie con Precio y Carrito -->
          <ion-card-content class="mercury-producto-cuerpo">
            <h3 class="mercury-producto-titulo">{{ p.nombre }}</h3>
            <p class="mercury-producto-categoria-texto">{{ p.categoria_nombre }}</p>
            <p class="mercury-producto-desc-texto">{{ p.descripcion || 'Sin descripción adicional' }}</p>

            <div class="mercury-producto-acciones-pie">
              <span class="mercury-producto-precio-grande">${{ p.precio.toLocaleString('es-AR') }}</span>
              <ion-button
                v-if="p.disponible"
                size="small"
                fill="solid"
                color="primary"
                class="mercury-btn-agregar"
                @click="agregarAlCarrito(p)"
              >
                <ion-icon slot="start" :icon="cartOutline" />
                Agregar
              </ion-button>
            </div>
          </ion-card-content>
        </ion-card>
      </div>
    </comp-lista>

    <!-- Modal para Agregar o Editar Producto (Admin) -->
    <comp-producto-modal
      :abierto="mostrarModalProducto"
      :producto="productoSeleccionadoParaEditar"
      @cerrar="mostrarModalProducto = false"
      @producto_guardado="alGuardarProducto"
    />

    <!-- Modal de Carrito -->
    <comp-carrito-modal
      :abierto="mostrarCarrito"
      @cerrar="mostrarCarrito = false"
      @pedido_creado="alCrearPedido"
    />

    <!-- Action Sheet para tomar foto o elegir de galería -->
    <ion-action-sheet
      :is-open="mostrarActionSheetFoto"
      header="Actualizar Foto de Mercadería"
      :sub-header="productoSeleccionadoParaFoto ? productoSeleccionadoParaFoto.nombre : ''"
      :buttons="botonesFoto"
      @didDismiss="mostrarActionSheetFoto = false"
    />

    <!-- Input file invisible para navegador web o selección de archivo -->
    <input
      ref="inputFotoProducto"
      type="file"
      accept="image/*"
      style="display: none;"
      @change="alSeleccionarFotoWeb"
    />

    <!-- Toast de Notificación -->
    <ion-toast
      :is-open="mostrarToast"
      :message="mensajeToast"
      :color="colorToast"
      duration="2500"
      @didDismiss="mostrarToast = false"
    />
  </comp-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import {
  IonItem,
  IonLabel,
  IonBadge,
  IonChip,
  IonIcon,
  IonButton,
  IonThumbnail,
  IonActionSheet,
  IonToast
} from '@ionic/vue';
import {
  gridOutline,
  cubeOutline,
  checkmarkCircleOutline,
  closeCircleOutline,
  cartOutline,
  cameraOutline,
  flameOutline,
  createOutline,
  addCircleOutline
} from 'ionicons/icons';

import CompPage from '../components/estructura/comp_page.vue';
import CompBuscador from '../components/base/comp_buscador.vue';
import CompLista from '../components/base/comp_lista.vue';
import CompEsqueleto from '../components/base/comp_esqueleto.vue';
import CompEstadoError from '../components/base/comp_estado_error.vue';
import CompEstadoVacio from '../components/base/comp_estado_vacio.vue';
import CompCarritoModal from '../components/dominio/pedidos/comp_carrito_modal.vue';
import CompProductoModal from '../components/dominio/productos/comp_producto_modal.vue';

import { productos_store } from '@/stores/productos_store';
import { productos_service } from '@/services/productos_service';
import { carrito_store } from '@/stores/carrito_store';
import { sesion_store } from '@/stores/sesion_store';
import { tomar_foto, camara_disponible } from '@/services/camara_service';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { resolver_url_imagen } from '@/utils/imagenes';

const busqueda = ref('');
const categoriaSeleccionada = ref(null);
const filtroDisponible = ref(null);
const filtroOferta = ref(null);

const mostrarCarrito = ref(false);
const mostrarModalProducto = ref(false);
const productoSeleccionadoParaEditar = ref(null);

const mostrarActionSheetFoto = ref(false);
const productoSeleccionadoParaFoto = ref(null);
const inputFotoProducto = ref(null);

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
  // Limpiar el mapa de errores para que la nueva imagen cargue de inmediato
  imagenesConError.value = {};
  await Promise.all([
    productos_store.cargar_resumen(),
    productos_store.cargar_listado(true)
  ]);
  mensajeToast.value = '¡Producto guardado exitosamente!';
  colorToast.value = 'success';
  mostrarToast.value = true;
};

const mostrarToast = ref(false);
const mensajeToast = ref('');
const colorToast = ref('success');

const imagenesConError = ref({});
const fallbackIntentado = ref({});

function obtenerSrcImagen(p) {
  if (fallbackIntentado.value[p.id]) {
    return fallbackIntentado.value[p.id];
  }
  return resolver_url_imagen(p.imagen_url);
}

const marcarErrorImagen = (p) => {
  if (p.imagen_url && p.imagen_url.startsWith('/seed/productos/') && !fallbackIntentado.value[p.id]) {
    const nombre = p.imagen_url.split('/').pop();
    fallbackIntentado.value[p.id] = `/assets/productos/${nombre}`;
    return;
  }
  imagenesConError.value[p.id] = true;
};

const botonesFoto = computed(() => {
  const botones = [];
  if (camara_disponible()) {
    botones.push({
      text: '📷 Tomar Foto con Cámara',
      handler: () => capturarFotoProducto('camara')
    });
    botones.push({
      text: '🖼 Elegir de Galería / Fotos',
      handler: () => capturarFotoProducto('galeria')
    });
  }
  botones.push({
    text: '📁 Seleccionar Archivo (PC / Web)',
    handler: () => {
      if (inputFotoProducto.value) {
        inputFotoProducto.value.click();
      }
    }
  });
  botones.push({
    text: 'Cancelar',
    role: 'cancel'
  });
  return botones;
});

const abrirOpcionesFoto = (producto) => {
  productoSeleccionadoParaFoto.value = producto;
  mostrarActionSheetFoto.value = true;
};

const capturarFotoProducto = async (origen) => {
  mostrarActionSheetFoto.value = false;
  vibrar_toque();
  const res = await tomar_foto(origen, { prefijo: 'producto', width: 900, quality: 65 });
  if (res.ok && res.archivo && productoSeleccionadoParaFoto.value) {
    await subirFotoProducto(productoSeleccionadoParaFoto.value, res.archivo);
  } else if (res.mensaje && !res.cancelado) {
    await vibrar_error();
    mensajeToast.value = res.mensaje;
    colorToast.value = 'warning';
    mostrarToast.value = true;
  }
};

const alSeleccionarFotoWeb = async (event) => {
  const archivo = event?.target?.files?.[0];
  if (archivo && productoSeleccionadoParaFoto.value) {
    await subirFotoProducto(productoSeleccionadoParaFoto.value, archivo);
  }
  if (event?.target) event.target.value = '';
};

const subirFotoProducto = async (producto, archivo) => {
  try {
    mensajeToast.value = 'Subiendo imagen del producto…';
    colorToast.value = 'primary';
    mostrarToast.value = true;

    const res = await productos_service.subir_imagen(producto.id, archivo);
    producto.imagen_url = res.imagen_url;

    // Desbloquear inmediatamente la imagen en la tarjeta del catálogo
    delete imagenesConError.value[producto.id];

    // Sincronizar también la vitrina y ofertas
    await productos_store.cargar_resumen();

    await vibrar_exito();
    mensajeToast.value = `¡Foto de '${producto.nombre}' actualizada exitosamente!`;
    colorToast.value = 'success';
    mostrarToast.value = true;
  } catch (err) {
    await vibrar_error();
    mensajeToast.value = err.mensaje || 'Error al subir la imagen del producto.';
    colorToast.value = 'danger';
    mostrarToast.value = true;
  }
};

const alBuscar = (texto) => {
  productos_store.establecer_busqueda(texto);
};

const filtrarCategoria = (catId) => {
  categoriaSeleccionada.value = catId;
  productos_store.establecer_categoria(catId);
};

const filtrarDisponibilidad = (valor) => {
  filtroDisponible.value = valor;
  filtroOferta.value = null;
  productos_store.establecer_oferta(null);
  productos_store.establecer_disponible(valor);
};

const toggleOferta = () => {
  filtroOferta.value = filtroOferta.value ? null : true;
  filtroDisponible.value = null;
  productos_store.establecer_disponible(null);
  productos_store.establecer_oferta(filtroOferta.value);
};

const limpiarFiltrosSecundarios = () => {
  filtroDisponible.value = null;
  filtroOferta.value = null;
  productos_store.establecer_oferta(null);
  productos_store.establecer_disponible(null);
};

const agregarAlCarrito = (producto) => {
  vibrar_toque();
  carrito_store.agregar_producto(producto, 1);
  mensajeToast.value = `¡${producto.nombre} agregado al carrito!`;
  colorToast.value = 'success';
  mostrarToast.value = true;
};

const alCrearPedido = (pedidoCreado) => {
  mensajeToast.value = `¡Orden ${pedidoCreado.numero} confirmada como Pendiente!`;
  colorToast.value = 'success';
  mostrarToast.value = true;
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

.mercury-btn-carrito {
  position: relative;
  --padding-start: 8px;
  --padding-end: 8px;
}

.mercury-badge-carrito {
  position: absolute;
  top: -2px;
  right: -2px;
  font-size: 0.72rem;
  border-radius: 10px;
  padding: 2px 6px;
}

/* Grilla de Tarjetas de Productos */
.mercury-productos-grid {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 4px 12px 16px 12px;
}

.mercury-card-producto {
  margin: 0;
  border-radius: 14px;
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.06);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.mercury-producto-img-bloque {
  position: relative;
  width: 100%;
  height: 150px;
  background: var(--ion-color-light, #f1f5f9);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.mercury-producto-foto-portada {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.mercury-producto-sin-foto {
  font-size: 3.5rem;
  color: var(--ion-color-medium, #94a3b8);
}

.mercury-badges-flotantes {
  position: absolute;
  top: 10px;
  left: 10px;
  display: flex;
  gap: 6px;
}

.mercury-flotante-badge {
  font-size: 0.72rem;
  font-weight: 700;
  padding: 4px 8px;
  border-radius: 6px;
}

.mercury-acciones-admin-flotantes {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  gap: 6px;
}

.mercury-btn-accion-flotante {
  --border-radius: 50%;
  width: 34px;
  height: 34px;
  opacity: 0.95;
}

.mercury-producto-cuerpo {
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
}

.mercury-producto-titulo {
  font-size: 1.05rem;
  font-weight: 700;
  margin: 0 0 2px 0;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-producto-categoria-texto {
  font-size: 0.78rem;
  font-weight: 600;
  color: var(--ion-color-primary, #2563eb);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0 0 4px 0;
}

.mercury-producto-desc-texto {
  font-size: 0.85rem;
  color: var(--ion-color-medium, #64748b);
  margin: 0 0 12px 0;
  line-height: 1.35;
}

.mercury-producto-acciones-pie {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: auto;
  border-top: 1px solid var(--ion-color-light, #f1f5f9);
  padding-top: 10px;
}

.mercury-producto-precio-grande {
  font-size: 1.25rem;
  font-weight: 800;
  color: var(--ion-color-dark, #0f172a);
}

.mercury-btn-agregar {
  --border-radius: 8px;
  font-weight: 600;
}
</style>