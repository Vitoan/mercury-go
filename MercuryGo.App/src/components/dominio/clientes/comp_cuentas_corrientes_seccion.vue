<template>
  <div class="mercury-finanzas-seccion">
    <!-- Tarjeta Resumen General -->
    <ion-card class="mercury-card-metricas">
      <ion-card-content>
        <div class="mercury-metricas-grid">
          <div class="mercury-metrica-caja">
            <span class="mercury-metrica-label">Deuda Total en Calle:</span>
            <strong class="mercury-metrica-valor text-warning">
              ${{ metricasFinanzas.deuda_total.toLocaleString('es-AR') }}
            </strong>
          </div>
          <div class="mercury-metrica-caja">
            <span class="mercury-metrica-label">Clientes con Saldo Deudor:</span>
            <strong class="mercury-metrica-valor">{{ metricasFinanzas.clientes_con_deuda }}</strong>
          </div>
        </div>
        <div v-if="metricasFinanzas.total_comprobantes_pendientes > 0" class="mercury-alerta-pendientes">
          <ion-icon :icon="timeOutline" color="warning" />
          <span>Hay <strong>{{ metricasFinanzas.total_comprobantes_pendientes }}</strong> comprobante(s) esperando tu aprobación.</span>
        </div>
      </ion-card-content>
    </ion-card>

    <!-- Spinner cargando finanzas -->
    <div v-if="cargandoFinanzas" class="mercury-finanzas-loading">
      <ion-spinner name="crescent" color="primary" />
      <p>Actualizando cuentas corrientes…</p>
    </div>

    <!-- Lista de Clientes con Estado Financiero -->
    <ion-list v-else class="mercury-finanzas-lista">
      <ion-card v-for="item in cuentasClientes" :key="item.cliente_id" class="mercury-card-cliente-cc">
        <ion-card-header>
          <div class="mercury-cc-cabecera">
            <div>
              <ion-card-title class="mercury-cc-titulo">{{ item.razon_social }}</ion-card-title>
              <ion-card-subtitle>CUIT: {{ item.cuit }} · {{ item.localidad || 'General' }}</ion-card-subtitle>
            </div>
            <ion-badge :color="item.saldo_deudor > 0 ? 'warning' : 'success'" class="mercury-badge-deuda">
              {{ item.saldo_deudor > 0 ? 'Debe: $' + item.saldo_deudor.toLocaleString('es-AR') : 'Al Día' }}
            </ion-badge>
          </div>
        </ion-card-header>
        <ion-card-content>
          <div class="mercury-cc-totales-fila">
            <span>Facturado: <strong>${{ item.total_facturado.toLocaleString('es-AR') }}</strong></span>
            <span>Acreditado: <strong class="text-success">${{ item.total_pagado.toLocaleString('es-AR') }}</strong></span>
          </div>

          <!-- Comprobantes pendientes de auditar de este cliente -->
          <div v-if="item.comprobantes_pendientes_conteo > 0" class="mercury-cc-comprobantes-box">
            <div class="mercury-box-titulo">
              <ion-icon :icon="cardOutline" color="warning" />
              <span>Comprobantes esperando aprobación ({{ item.comprobantes_pendientes_conteo }}):</span>
            </div>

            <div
              v-for="comp in item.comprobantes_pendientes"
              :key="comp.pedido_id"
              class="mercury-item-comprobante-fila"
            >
              <div class="mercury-comp-info">
                <strong>Orden {{ comp.pedido_numero }}</strong>
                <span>Monto: ${{ comp.total.toLocaleString('es-AR') }}</span>
              </div>
              <div class="mercury-comp-botones">
                <ion-button
                  v-if="comp.comprobante_url"
                  size="small"
                  fill="outline"
                  color="tertiary"
                  @click="verArchivoComprobante(comp.comprobante_url)"
                >
                  Ver Foto / PDF
                </ion-button>
                <ion-button
                  size="small"
                  color="success"
                  @click="aprobarComprobanteDirecto(comp)"
                >
                  ✔ Aprobar
                </ion-button>
                <ion-button
                  size="small"
                  color="danger"
                  fill="outline"
                  @click="rechazarComprobanteDirecto(comp.pedido_id)"
                >
                  ✖ Rechazar
                </ion-button>
              </div>
            </div>
          </div>
        </ion-card-content>
      </ion-card>
    </ion-list>

    <!-- Toast local para feedback de auditoría bancaria -->
    <ion-toast
      :is-open="toast.visible.value"
      :message="toast.mensaje.value"
      :color="toast.color.value"
      duration="3000"
      @didDismiss="toast.ocultar"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import {
  IonCard,
  IonCardHeader,
  IonCardTitle,
  IonCardSubtitle,
  IonCardContent,
  IonList,
  IonBadge,
  IonButton,
  IonIcon,
  IonSpinner,
  IonToast,
  alertController
} from '@ionic/vue';
import {
  timeOutline,
  cardOutline
} from 'ionicons/icons';

import { pedidos_service } from '@/services/pedidos_service';
import { resolver_url_imagen } from '@/utils/imagenes';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { useToast } from '@/composables/useToast';

const emit = defineEmits(['metricas_actualizadas']);

const toast = useToast();
const cargandoFinanzas = ref(false);
const cuentasClientes = ref([]);
const metricasFinanzas = ref({
  deuda_total: 0,
  clientes_con_deuda: 0,
  total_comprobantes_pendientes: 0
});

const cargarFinanzas = async () => {
  cargandoFinanzas.value = true;
  try {
    const res = await pedidos_service.obtener_cuentas_corrientes_clientes();
    cuentasClientes.value = res.clientes || [];
    metricasFinanzas.value = res.metricas || {
      deuda_total: 0,
      clientes_con_deuda: 0,
      total_comprobantes_pendientes: 0
    };
    emit('metricas_actualizadas', metricasFinanzas.value);
  } catch (err) {
    console.error('Error al cargar cuentas corrientes:', err);
  } finally {
    cargandoFinanzas.value = false;
  }
};

const verArchivoComprobante = (urlRelativa) => {
  vibrar_toque();
  const urlCompleta = resolver_url_imagen(urlRelativa);
  if (urlCompleta) window.open(urlCompleta, '_blank');
};

const aprobarComprobanteDirecto = async (comp) => {
  const pedidoId = comp?.pedido_id || comp;
  const montoSugerido = comp?.total || 0;

  const alert = await alertController.create({
    header: 'Auditoría Bancaria',
    subHeader: `Orden ${comp?.pedido_numero || pedidoId}`,
    message: 'Ingresá el monto exacto acreditado en la cuenta de la empresa:',
    inputs: [
      {
        name: 'monto',
        type: 'number',
        placeholder: 'Monto acreditado',
        value: montoSugerido.toString(),
        min: 1
      }
    ],
    buttons: [
      {
        text: 'Cancelar',
        role: 'cancel'
      },
      {
        text: 'Aprobar Pago',
        handler: async (data) => {
          const rawMonto = data?.monto;
          if (!rawMonto) {
            toast.mostrarError('Por favor ingresá un monto válido mayor a 0.');
            return false;
          }

          const monto = parseFloat(String(rawMonto).replace(/\./g, '').replace(',', '.'));
          if (isNaN(monto) || monto <= 0) {
            toast.mostrarError('Por favor ingresá un monto válido mayor a 0.');
            return false;
          }

          try {
            vibrar_toque();
            await pedidos_service.aprobar_comprobante(pedidoId, monto);
            await vibrar_exito();
            toast.mostrarExito('¡Comprobante aprobado exitosamente!');
            await cargarFinanzas();
            return true;
          } catch (err) {
            await vibrar_error();
            toast.mostrarError(err.mensaje || err.message || 'No se pudo aprobar el comprobante.');
            return false;
          }
        }
      }
    ]
  });

  await alert.present();
};

const rechazarComprobanteDirecto = async (pedidoId) => {
  const alert = await alertController.create({
    header: 'Rechazar Comprobante',
    message: 'Ingresá el motivo del rechazo del comprobante de pago:',
    inputs: [
      {
        name: 'motivo',
        type: 'text',
        placeholder: 'Motivo de rechazo',
        value: 'Comprobante no acreditado en cuenta'
      }
    ],
    buttons: [
      {
        text: 'Cancelar',
        role: 'cancel'
      },
      {
        text: 'Rechazar Pago',
        role: 'destructive',
        handler: async (data) => {
          const motivo = data?.motivo?.trim();
          if (!motivo) {
            toast.mostrarError('Por favor especificá el motivo del rechazo.');
            return false;
          }

          try {
            vibrar_toque();
            await pedidos_service.rechazar_comprobante(pedidoId, motivo);
            await vibrar_exito();
            toast.mostrarExito('Comprobante rechazado correctamente.');
            await cargarFinanzas();
            return true;
          } catch (err) {
            await vibrar_error();
            toast.mostrarError(err.mensaje || err.message || 'No se pudo rechazar el comprobante.');
            return false;
          }
        }
      }
    ]
  });

  await alert.present();
};

onMounted(() => {
  cargarFinanzas();
});

defineExpose({
  cargarFinanzas,
  metricasFinanzas
});
</script>

<style scoped>
.mercury-finanzas-seccion {
  padding: 0 12px 24px 12px;
}

.mercury-card-metricas {
  margin: 0 0 16px 0;
  border-radius: 12px;
  background: var(--ion-color-step-50, #f8fafc);
}

.mercury-metricas-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.mercury-metrica-caja {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.mercury-metrica-label {
  font-size: 0.8rem;
  color: var(--ion-color-step-600, #64748b);
}

.mercury-metrica-valor {
  font-size: 1.25rem;
  font-weight: 800;
}

.mercury-alerta-pendientes {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px dashed var(--ion-color-step-200, #e2e8f0);
  font-size: 0.88rem;
}

.mercury-finanzas-loading {
  text-align: center;
  padding: 30px;
  color: var(--ion-color-medium, #64748b);
}

.mercury-card-cliente-cc {
  margin: 0 0 14px 0;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.mercury-cc-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 8px;
}

.mercury-cc-titulo {
  font-size: 1.05rem;
  font-weight: 700;
}

.mercury-badge-deuda {
  font-size: 0.85rem;
  padding: 6px 10px;
  border-radius: 6px;
}

.mercury-cc-totales-fila {
  display: flex;
  justify-content: space-between;
  font-size: 0.9rem;
  margin-bottom: 10px;
}

.mercury-cc-comprobantes-box {
  margin-top: 10px;
  padding: 10px;
  border-radius: 8px;
  background: var(--ion-color-step-100, rgba(0, 0, 0, 0.04));
  border-left: 3px solid var(--ion-color-warning, #f59e0b);
}

.mercury-box-titulo {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  font-weight: 700;
  margin-bottom: 8px;
}

.mercury-item-comprobante-fila {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  padding: 8px 0;
  border-top: 1px dashed var(--ion-color-step-200, rgba(0, 0, 0, 0.08));
}

.mercury-comp-info {
  display: flex;
  flex-direction: column;
  font-size: 0.85rem;
}

.mercury-comp-botones {
  display: flex;
  gap: 6px;
}
</style>
