<template>
  <ion-modal :is-open="abierto" @didDismiss="$emit('cerrar')">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>{{ producto ? 'Editar Mercadería' : 'Nuevo Producto' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="$emit('cerrar')">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div class="mercury-modal-stack">
        <!-- Bloque de Foto del Producto -->
        <div class="mercury-foto-bloque">
          <div class="mercury-foto-preview">
            <img
              v-if="imagenUrlActual"
              :src="resolver_url_imagen(imagenUrlActual)"
              alt="Foto Producto"
              class="mercury-foto-img"
              @error="imagenUrlActual = null"
            />
            <div v-else class="mercury-foto-sin-img">
              <ion-icon :icon="cubeOutline" />
            </div>
            <div v-if="subiendoFoto" class="mercury-foto-loading">
              <ion-spinner name="crescent" color="primary" />
            </div>
          </div>

          <ion-button
            size="small"
            fill="outline"
            color="primary"
            class="mercury-btn-foto-accion"
            :disabled="subiendoFoto"
            @click="mostrarActionSheetFoto = true"
          >
            <ion-icon slot="start" :icon="cameraOutline" />
            {{ imagenUrlActual ? 'Cambiar Foto' : 'Cargar Foto' }}
          </ion-button>
        </div>

        <div class="mercury-modal-subtitulo">
          <p>Defina las propiedades del producto para la lista oficial y el catálogo comercial de MercuryGO.</p>
        </div>

        <ion-list class="mercury-form-list">
          <ion-item class="mercury-form-item">
            <ion-input
              v-model="formulario.nombre"
              label="Nombre del Producto / Marca *"
              label-placement="stacked"
              placeholder="Ej: Aceite de Girasol 1.5L"
              required
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-select
              v-model="formulario.categoria_id"
              label="Categoría *"
              label-placement="stacked"
              placeholder="Seleccionar categoría"
              interface="action-sheet"
              cancel-text="Cancelar"
            >
              <ion-select-option
                v-for="cat in categorias"
                :key="cat.id"
                :value="cat.id"
              >
                {{ cat.nombre }}
              </ion-select-option>
            </ion-select>
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-input
              v-model.number="formulario.precio"
              type="number"
              label="Precio Unitario ($) *"
              label-placement="stacked"
              placeholder="0.00"
              required
            />
          </ion-item>

          <ion-item class="mercury-form-item">
            <ion-textarea
              v-model="formulario.descripcion"
              label="Descripción / Presentación"
              label-placement="stacked"
              placeholder="Ej: Caja x 12 unidades, bulto cerrado"
              :rows="2"
            />
          </ion-item>

          <!-- Toggle: Disponible en stock -->
          <ion-item class="mercury-form-item" lines="inset">
            <ion-label>
              <h3>En Stock / Disponible</h3>
              <p>Permite a los clientes agregarlo a pedidos</p>
            </ion-label>
            <ion-toggle
              slot="end"
              :checked="formulario.disponible"
              @ionChange="formulario.disponible = $event.detail.checked"
            />
          </ion-item>

          <!-- Toggle: En Oferta Destacada -->
          <ion-item class="mercury-form-item" lines="none">
            <ion-label>
              <h3>🔥 En Oferta Especial</h3>
              <p>Se destaca en Inicio y en el filtro de ofertas</p>
            </ion-label>
            <ion-toggle
              slot="end"
              color="danger"
              :checked="formulario.en_oferta"
              @ionChange="formulario.en_oferta = $event.detail.checked"
            />
          </ion-item>
        </ion-list>

        <!-- Mensaje de error si la validación falla -->
        <div v-if="errorMensaje" class="mercury-error-aviso">
          <ion-icon :icon="alertCircleOutline" />
          <span>{{ errorMensaje }}</span>
        </div>

        <div class="mercury-modal-footer">
          <ion-button
            expand="block"
            color="success"
            :disabled="!esValido || guardando || subiendoFoto"
            @click="guardar"
          >
            <ion-spinner v-if="guardando" name="dots" />
            <span v-else>{{ producto ? 'Actualizar Producto' : 'Crear Producto' }}</span>
          </ion-button>
        </div>
      </div>

      <!-- Action Sheet para tomar foto o elegir de galería -->
      <ion-action-sheet
        :is-open="mostrarActionSheetFoto"
        header="Foto del Producto"
        :buttons="botonesFoto"
        @didDismiss="mostrarActionSheetFoto = false"
      />

      <!-- Input file invisible para navegador web o selección de archivo -->
      <input
        ref="inputFoto"
        type="file"
        accept="image/*"
        style="display: none;"
        @change="alSeleccionarFotoWeb"
      />
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import {
  IonModal,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonButtons,
  IonButton,
  IonContent,
  IonList,
  IonItem,
  IonInput,
  IonTextarea,
  IonSelect,
  IonSelectOption,
  IonToggle,
  IonLabel,
  IonIcon,
  IonSpinner,
  IonActionSheet
} from '@ionic/vue';
import { alertCircleOutline, cameraOutline, cubeOutline } from 'ionicons/icons';
import { productos_store } from '@/stores/productos_store';
import { productos_service } from '@/services/productos_service';
import { tomar_foto, camara_disponible } from '@/services/camara_service';
import { resolver_url_imagen } from '@/utils/imagenes';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';

const props = defineProps({
  abierto: { type: Boolean, default: false },
  producto: { type: Object, default: null }
});

const emit = defineEmits(['cerrar', 'producto_guardado']);

const guardando = ref(false);
const subiendoFoto = ref(false);
const errorMensaje = ref('');
const mostrarActionSheetFoto = ref(false);
const inputFoto = ref(null);
const imagenUrlActual = ref(null);
const archivoFotoPendiente = ref(null);

const formulario = ref({
  nombre: '',
  categoria_id: null,
  descripcion: '',
  precio: 0,
  disponible: true,
  en_oferta: false
});

const categorias = computed(() => productos_store.categorias);

const esValido = computed(() => {
  return (
    formulario.value.nombre?.trim().length > 0 &&
    formulario.value.categoria_id &&
    formulario.value.precio > 0
  );
});

const botonesFoto = computed(() => {
  const botones = [];
  if (camara_disponible()) {
    botones.push({
      text: '📷 Tomar Foto con Cámara',
      handler: () => capturarFoto('camara')
    });
    botones.push({
      text: '🖼 Elegir de Galería / Fotos',
      handler: () => capturarFoto('galeria')
    });
  }
  botones.push({
    text: '📁 Seleccionar Archivo (PC / Web)',
    handler: () => {
      inputFoto.value?.click();
    }
  });
  botones.push({
    text: 'Cancelar',
    role: 'cancel'
  });
  return botones;
});

watch(
  () => props.producto,
  (prod) => {
    if (prod) {
      formulario.value = {
        nombre: prod.nombre || '',
        categoria_id: prod.categoria_id || (categorias.value[0]?.id ?? null),
        descripcion: prod.descripcion || '',
        precio: prod.precio || 0,
        disponible: prod.disponible ?? true,
        en_oferta: prod.en_oferta ?? false
      };
      imagenUrlActual.value = prod.imagen_url || null;
    } else {
      formulario.value = {
        nombre: '',
        categoria_id: categorias.value[0]?.id ?? 1,
        descripcion: '',
        precio: 0,
        disponible: true,
        en_oferta: false
      };
      imagenUrlActual.value = null;
    }
    archivoFotoPendiente.value = null;
    errorMensaje.value = '';
  },
  { immediate: true }
);

const capturarFoto = async (origen) => {
  mostrarActionSheetFoto.value = false;
  vibrar_toque();
  const res = await tomar_foto(origen);
  if (res.ok && res.archivo) {
    await procesarArchivoFoto(res.archivo);
  } else if (res.mensaje && !res.cancelado) {
    await vibrar_error();
    errorMensaje.value = res.mensaje;
  }
};

const alSeleccionarFotoWeb = async (event) => {
  const archivo = event?.target?.files?.[0];
  if (archivo) {
    await procesarArchivoFoto(archivo);
  }
  if (event?.target) event.target.value = '';
};

const procesarArchivoFoto = async (archivo) => {
  if (props.producto?.id) {
    // Si el producto ya existe en la base de datos, subir de inmediato
    subiendoFoto.value = true;
    try {
      const res = await productos_service.subir_imagen(props.producto.id, archivo);
      imagenUrlActual.value = res.imagen_url;
      props.producto.imagen_url = res.imagen_url;
      await vibrar_exito();
    } catch (err) {
      await vibrar_error();
      errorMensaje.value = err.message || 'Error al subir la foto del producto.';
    } finally {
      subiendoFoto.value = false;
    }
  } else {
    // Si es un producto nuevo que aún no se creó, guardar el archivo temporalmente
    archivoFotoPendiente.value = archivo;
    imagenUrlActual.value = URL.createObjectURL(archivo);
    vibrar_toque();
  }
};

const guardar = async () => {
  if (!esValido.value) return;
  guardando.value = true;
  errorMensaje.value = '';

  try {
    const payload = {
      nombre: formulario.value.nombre.trim(),
      categoria_id: formulario.value.categoria_id,
      descripcion: formulario.value.descripcion?.trim() || null,
      precio: Number(formulario.value.precio),
      disponible: Boolean(formulario.value.disponible),
      en_oferta: Boolean(formulario.value.en_oferta)
    };

    if (props.producto?.id) {
      await productos_store.guardar_producto(payload, props.producto.id);
    } else {
      const res = await productos_service.crear(payload);
      const nuevoId = res.producto?.id;
      if (nuevoId && archivoFotoPendiente.value) {
        await productos_service.subir_imagen(nuevoId, archivoFotoPendiente.value);
      }
      await productos_store.cargar_resumen();
      await productos_store.cargar_listado(true);
    }

    await vibrar_exito();
    emit('producto_guardado');
    emit('cerrar');
  } catch (err) {
    await vibrar_error();
    errorMensaje.value = err.message || 'Error al guardar el producto.';
  } finally {
    guardando.value = false;
  }
};
</script>

<style scoped>
.mercury-modal-stack {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.mercury-foto-bloque {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  padding: 10px 0;
}

.mercury-foto-preview {
  position: relative;
  width: 140px;
  height: 140px;
  border-radius: 14px;
  background: var(--ion-color-light, #f1f5f9);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.08);
}

.mercury-foto-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.mercury-foto-sin-img {
  font-size: 3.5rem;
  color: var(--ion-color-medium, #94a3b8);
}

.mercury-foto-loading {
  position: absolute;
  inset: 0;
  background: rgba(255, 255, 255, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
}

.mercury-btn-foto-accion {
  --border-radius: 8px;
  font-weight: 600;
}

.mercury-modal-subtitulo p {
  margin: 0;
  font-size: 0.88rem;
  color: var(--ion-color-medium, #64748b);
  line-height: 1.4;
}

.mercury-form-list {
  background: transparent;
  padding: 0;
}

.mercury-form-item {
  --background: var(--ion-color-light, #f8fafc);
  margin-bottom: 10px;
  border-radius: 8px;
}

.mercury-error-aviso {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  background: rgba(239, 68, 68, 0.1);
  color: var(--ion-color-danger, #ef4444);
  border-radius: 8px;
  font-size: 0.88rem;
}

.mercury-modal-footer {
  margin-top: 10px;
}
</style>
