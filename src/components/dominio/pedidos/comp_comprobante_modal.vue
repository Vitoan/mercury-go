<template>
  <ion-modal :is-open="abierto" @didDismiss="cerrar">
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Comprobante de Transferencia</ion-title>
        <ion-buttons slot="end">
          <ion-button @click="cerrar">Cerrar</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div v-if="pedido" class="mercury-comprobante-header">
        <h2>Orden {{ pedido.numero }}</h2>
        <p class="mercury-comprobante-total">Total a transferir: <strong>${{ pedido.total?.toLocaleString('es-AR') }}</strong></p>
      </div>

      <!-- Datos bancarios de la distribuidora para la transferencia -->
      <ion-card class="mercury-card-banco">
        <ion-card-header>
          <ion-card-subtitle>Datos para transferencia bancaria</ion-card-subtitle>
          <ion-card-title>MercuryGO Distribución S.A.</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <p><strong>Banco:</strong> Banco Nación Argentina</p>
          <p><strong>CBU:</strong> 0110485630048512345678</p>
          <p><strong>Alias:</strong> MERCURYGO.DISTRIB</p>
          <p><strong>CUIT:</strong> 30-71999888-4</p>
        </ion-card-content>
      </ion-card>

      <!-- Previsualización del archivo seleccionado -->
      <div v-if="archivoSeleccionado" class="mercury-archivo-previo">
        <ion-icon :icon="documentAttachOutline" class="mercury-archivo-icono" />
        <div class="mercury-archivo-info">
          <strong>{{ archivoSeleccionado.name }}</strong>
          <span>({{ (archivoSeleccionado.size / 1024).toFixed(1) }} KB)</span>
        </div>
        <ion-button fill="clear" color="danger" size="small" @click="quitarArchivo">
          <ion-icon :icon="trashOutline" />
        </ion-button>
      </div>

      <!-- Botones para capturar/subir comprobante -->
      <div class="mercury-opciones-captura">
        <ion-button
          v-if="esNativo"
          expand="block"
          fill="outline"
          color="primary"
          @click="capturarFoto"
        >
          <ion-icon slot="start" :icon="cameraOutline" />
          Sacar Foto al Comprobante
        </ion-button>

        <ion-button
          v-if="esNativo"
          expand="block"
          fill="outline"
          color="secondary"
          @click="elegirGaleria"
        >
          <ion-icon slot="start" :icon="imagesOutline" />
          Elegir de Galería / Fotos
        </ion-button>

        <!-- Selector web universal (también para PDFs en móvil) -->
        <label class="mercury-boton-archivo-label">
          <ion-button expand="block" fill="outline" color="tertiary" @click="activarInput">
            <ion-icon slot="start" :icon="documentTextOutline" />
            Adjuntar Archivo o PDF
          </ion-button>
          <input
            ref="inputArchivo"
            type="file"
            accept="image/*,application/pdf"
            style="display: none;"
            @change="alSeleccionarArchivoWeb"
          />
        </label>
      </div>

      <!-- Mensaje de error si hubo -->
      <div v-if="errorMensaje" class="mercury-error-aviso">
        <ion-icon :icon="alertCircleOutline" />
        <span>{{ errorMensaje }}</span>
      </div>

      <!-- Botón de Envío -->
      <div style="margin-top: 24px;">
        <ion-button
          expand="block"
          color="success"
          :disabled="!archivoSeleccionado || subiendo"
          @click="enviarComprobante"
        >
          <ion-spinner v-if="subiendo" name="dots" />
          <span v-else>Confirmar y Enviar Comprobante</span>
        </ion-button>
      </div>
    </ion-content>
  </ion-modal>
</template>

<script setup>
import { ref, computed } from 'vue';
import {
  IonModal,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonButtons,
  IonButton,
  IonContent,
  IonCard,
  IonCardHeader,
  IonCardSubtitle,
  IonCardTitle,
  IonCardContent,
  IonIcon,
  IonSpinner
} from '@ionic/vue';
import {
  cameraOutline,
  imagesOutline,
  documentTextOutline,
  documentAttachOutline,
  trashOutline,
  alertCircleOutline
} from 'ionicons/icons';
import { camara_disponible, tomar_foto } from '@/services/camara_service';
import { pedidos_service } from '@/services/pedidos_service';
import { vibrar_exito, vibrar_error } from '@/services/vibracion_service';

const props = defineProps({
  abierto: { type: Boolean, default: false },
  pedido: { type: Object, default: null }
});

const emit = defineEmits(['cerrar', 'comprobante_subido']);

const esNativo = computed(() => camara_disponible());
const archivoSeleccionado = ref(null);
const subiendo = ref(false);
const errorMensaje = ref('');
const inputArchivo = ref(null);

const cerrar = () => {
  archivoSeleccionado.value = null;
  errorMensaje.value = '';
  emit('cerrar');
};

const quitarArchivo = () => {
  archivoSeleccionado.value = null;
};

const activarInput = () => {
  inputArchivo.value?.click();
};

const alSeleccionarArchivoWeb = (event) => {
  const files = event.target?.files;
  if (files && files[0]) {
    archivoSeleccionado.value = files[0];
    errorMensaje.value = '';
  }
};

const capturarFoto = async () => {
  errorMensaje.value = '';
  const res = await tomar_foto('camara');
  if (res.ok && res.archivo) {
    archivoSeleccionado.value = res.archivo;
  } else if (!res.cancelado && res.mensaje) {
    errorMensaje.value = res.mensaje;
  }
};

const elegirGaleria = async () => {
  errorMensaje.value = '';
  const res = await tomar_foto('galeria');
  if (res.ok && res.archivo) {
    archivoSeleccionado.value = res.archivo;
  } else if (!res.cancelado && res.mensaje) {
    errorMensaje.value = res.mensaje;
  }
};

const enviarComprobante = async () => {
  if (!props.pedido || !archivoSeleccionado.value) return;

  subiendo.value = true;
  errorMensaje.value = '';

  try {
    const res = await pedidos_service.subir_comprobante_pago(props.pedido.id, archivoSeleccionado.value);
    vibrar_exito();
    emit('comprobante_subido', res);
    cerrar();
  } catch (err) {
    vibrar_error();
    errorMensaje.value = err.message || 'No se pudo subir el comprobante.';
  } finally {
    subiendo.value = false;
  }
};
</script>

<style scoped>
.mercury-comprobante-header {
  text-align: center;
  margin-bottom: 14px;
}

.mercury-comprobante-header h2 {
  font-size: 1.4rem;
  font-weight: 700;
  margin: 0;
}

.mercury-comprobante-total {
  font-size: 1.05rem;
  color: var(--ion-color-step-700, #334155);
  margin-top: 4px;
}

.mercury-card-banco {
  border-radius: 12px;
  background: var(--ion-color-step-50, #f8fafc);
  margin: 0 0 16px 0;
}

.mercury-opciones-captura {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 14px;
}

.mercury-archivo-previo {
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--ion-color-step-100, #f1f5f9);
  padding: 10px 14px;
  border-radius: 8px;
  margin-bottom: 12px;
}

.mercury-archivo-icono {
  font-size: 1.8rem;
  color: var(--ion-color-primary, #38bdf8);
}

.mercury-archivo-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  font-size: 0.88rem;
}

.mercury-error-aviso {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(var(--ion-color-danger-rgb, 239, 68, 68), 0.1);
  color: var(--ion-color-danger, #ef4444);
  padding: 10px;
  border-radius: 8px;
  margin-top: 14px;
  font-size: 0.88rem;
}
</style>
