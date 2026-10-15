<template>
  <comp-page titulo="Mi cuenta">
    <div class="cuenta-stack">
      <!-- Tarjeta de Usuario y Rol -->
      <ion-card class="mercury-card-perfil">
        <ion-card-content class="mercury-perfil-compacto">
          <div class="mercury-perfil-principal">
            <ion-icon :icon="personCircleOutline" class="mercury-avatar-icon" />
            <div class="mercury-perfil-textos">
              <div class="mercury-perfil-fila-nombre">
                <h2>{{ sesion_store.usuario?.nombre || 'Usuario Conectado' }}</h2>
                <ion-badge
                  :color="badgeColor"
                  class="mercury-badge-rol"
                >
                  {{ sesion_store.usuario?.rol_nombre || 'Sin Habilitar' }}
                </ion-badge>
              </div>
              <p class="mercury-perfil-email">{{ sesion_store.usuario?.email || 'Sin correo' }}</p>
              <p v-if="sesion_store.usuario?.telefono" class="mercury-perfil-tel">📞 {{ sesion_store.usuario.telefono }}</p>
            </div>
          </div>

          <!-- Aviso para usuario sin rol (pendiente de habilitación) -->
          <div v-if="sesion_store.pendiente_de_habilitacion" class="mercury-aviso-pendiente">
            <ion-icon :icon="alertCircleOutline" />
            <div>
              <strong>Tu cuenta aún no tiene un rol asignado.</strong>
              <p>Un administrador de MercuryGO debe habilitarte para acceder a Catálogo, Clientes o Despachos.</p>
            </div>
          </div>

          <ion-button
            expand="block"
            color="danger"
            fill="outline"
            style="margin-top: 1rem;"
            @click="confirmarCerrarSesion"
          >
            <ion-icon slot="start" :icon="logOutOutline" />
            Cerrar Sesión
          </ion-button>
        </ion-card-content>
      </ion-card>

      <!-- Tarjeta de Cuenta Corriente y Saldo Deudor (Exclusivo para CLIENTE) -->
      <ion-card v-if="sesion_store.rol_activo === 'CLIENTE'" class="mercury-card-cuenta-corriente">
        <ion-card-header>
          <div class="mercury-cc-cabecera">
            <div>
              <ion-card-subtitle>Estado Financiero B2B</ion-card-subtitle>
              <ion-card-title>Cuenta Corriente</ion-card-title>
            </div>
            <ion-badge :color="cuentaCorriente?.saldo_deudor > 0 ? 'warning' : 'success'">
              {{ cuentaCorriente?.saldo_deudor > 0 ? 'Saldo Deudor' : 'Al Día' }}
            </ion-badge>
          </div>
        </ion-card-header>
        <ion-card-content>
          <div v-if="cargandoCC" class="mercury-cc-loading">
            <ion-spinner name="dots" color="primary" />
            <span>Consultando estado contable…</span>
          </div>
          <div v-else>
            <div class="mercury-cc-balance-caja">
              <span class="mercury-cc-balance-label">Total Adeudado:</span>
              <strong class="mercury-cc-balance-monto">
                ${{ (cuentaCorriente?.saldo_deudor || 0).toLocaleString('es-AR') }}
              </strong>
            </div>

            <div class="mercury-cc-desglose">
              <div class="mercury-cc-fila">
                <span>Total Facturado:</span>
                <span>${{ (cuentaCorriente?.total_facturado || 0).toLocaleString('es-AR') }}</span>
              </div>
              <div class="mercury-cc-fila">
                <span>Comprobantes Acreditados:</span>
                <span class="text-success">${{ (cuentaCorriente?.total_pagado || 0).toLocaleString('es-AR') }}</span>
              </div>
              <div v-if="cuentaCorriente?.comprobantes_pendientes > 0" class="mercury-cc-fila">
                <span>Comprobantes en Revisión:</span>
                <span class="text-warning">⏳ {{ cuentaCorriente.comprobantes_pendientes }} a la espera de aprobación</span>
              </div>
              <div class="mercury-cc-fila">
                <span>Pedidos Pendientes de Entrega:</span>
                <span>{{ cuentaCorriente?.pedidos_pendientes || 0 }}</span>
              </div>
            </div>

            <div class="mercury-cc-acciones">
              <ion-button
                expand="block"
                color="success"
                @click="abrirSubidaComprobante"
              >
                <ion-icon slot="start" :icon="cardOutline" />
                Cargar Comprobante de Pago
              </ion-button>
            </div>
          </div>
        </ion-card-content>
      </ion-card>

      <!-- Modal de Carga de Comprobante de Pago Directo -->
      <comp-comprobante-modal
        :abierto="mostrarModalComprobante"
        :pedido="{ id: 1, numero: 'PAGO-CTA-CTE', total: cuentaCorriente?.saldo_deudor || 0 }"
        @cerrar="mostrarModalComprobante = false"
        @comprobante_subido="alSubirComprobanteCC"
      />

      <!-- Tarjeta de Preferencias y Apariencia -->
      <ion-card>
        <ion-card-header>
          <ion-card-subtitle>Preferencias</ion-card-subtitle>
          <ion-card-title>Tema y Respuesta Táctil</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="inset">
            <ion-icon slot="start" :icon="contrastOutline" />
            <ion-label>Modo oscuro</ion-label>
            <ion-toggle slot="end" :checked="modoOscuro" @ionChange="toggleTema" />
          </ion-item>
          <ion-item lines="none">
            <ion-icon slot="start" :icon="phonePortraitOutline" />
            <ion-label>
              <h3>Vibración y Respuesta Táctil</h3>
              <p>Feedback al tocar botones y confirmar acciones</p>
            </ion-label>
            <ion-toggle slot="end" :checked="vibracionActiva" @ionChange="toggleVibracion" />
          </ion-item>
        </ion-card-content>
      </ion-card>
    </div>
  </comp-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { 
  IonCard, IonCardHeader, IonCardSubtitle, IonCardTitle, IonCardContent,
  IonItem, IonLabel, IonIcon, IonToggle, IonButton, IonSpinner, IonBadge 
} from '@ionic/vue';
import { 
  personCircleOutline, contrastOutline, 
  alertCircleOutline, logOutOutline,
  phonePortraitOutline, cardOutline
} from 'ionicons/icons';
import CompPage from '../components/estructura/comp_page.vue';
import CompComprobanteModal from '../components/dominio/pedidos/comp_comprobante_modal.vue';
import { alternar_tema, es_tema_oscuro } from '../config/tema';
import { alternar_vibracion, es_vibracion_activa, vibrar_toque } from '../services/vibracion_service';
import { sesion_store } from '@/stores/sesion_store';
import { pedidos_service } from '@/services/pedidos_service';

const router = useRouter();

const modoOscuro = ref(false);
const vibracionActiva = ref(true);

const cuentaCorriente = ref(null);
const cargandoCC = ref(false);
const mostrarModalComprobante = ref(false);

const cargarCuentaCorriente = async () => {
  if (sesion_store.rol_activo !== 'CLIENTE') return;
  cargandoCC.value = true;
  try {
    const res = await pedidos_service.obtener_cuenta_corriente();
    cuentaCorriente.value = res;
  } catch {
    // Silencioso si no aplica
  } finally {
    cargandoCC.value = false;
  }
};

const abrirSubidaComprobante = () => {
  vibrar_toque();
  mostrarModalComprobante.value = true;
};

const alSubirComprobanteCC = async () => {
  mostrarModalComprobante.value = false;
  await cargarCuentaCorriente();
};

const badgeColor = computed(() => {
  const rol = sesion_store.rol_activo;
  switch (rol) {
    case 'ADMIN': return 'primary';
    case 'OPERARIO': return 'warning';
    case 'CHOFER': return 'tertiary';
    case 'CLIENTE': return 'success';
    default: return 'medium';
  }
});

const toggleTema = () => {
  modoOscuro.value = alternar_tema();
};

const toggleVibracion = () => {
  vibracionActiva.value = alternar_vibracion();
};

const confirmarCerrarSesion = async () => {
  await sesion_store.salir();
  router.replace('/login');
};

onMounted(() => {
  modoOscuro.value = es_tema_oscuro();
  vibracionActiva.value = es_vibracion_activa();
  cargarCuentaCorriente();
});
</script>

<style scoped>
.cuenta-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mercury-card-cuenta-corriente {
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
}

.mercury-cc-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.mercury-cc-loading {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--ion-color-medium, #64748b);
  padding: 8px 0;
}

.mercury-cc-balance-caja {
  background: var(--ion-color-light, #f8fafc);
  border-radius: 10px;
  padding: 14px;
  margin-bottom: 14px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.mercury-cc-balance-label {
  font-size: 0.85rem;
  color: var(--ion-color-medium, #64748b);
  font-weight: 500;
}

.mercury-cc-balance-monto {
  font-size: 1.6rem;
  font-weight: 800;
  color: var(--ion-color-danger, #ef4444);
}

.mercury-cc-desglose {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 0.9rem;
  margin-bottom: 16px;
}

.mercury-cc-fila {
  display: flex;
  justify-content: space-between;
  color: var(--ion-color-medium, #64748b);
}

.text-success {
  color: var(--ion-color-success, #16a34a);
  font-weight: 600;
}

.mercury-cc-acciones {
  margin-top: 10px;
}

.mercury-perfil-compacto {
  padding: 16px;
}

.mercury-perfil-principal {
  display: flex;
  align-items: center;
  gap: 14px;
}

.mercury-avatar-icon {
  font-size: 48px;
  color: var(--mercury-accent);
  flex-shrink: 0;
}

.mercury-perfil-textos {
  flex: 1;
  min-width: 0;
}

.mercury-perfil-fila-nombre {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}

.mercury-perfil-fila-nombre h2 {
  font-size: 1.15rem;
  font-weight: 700;
  margin: 0;
  color: var(--mercury-text-title);
}

.mercury-badge-rol {
  font-size: 0.72rem;
  padding: 3px 8px;
  font-weight: 700;
  border-radius: 6px;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

.mercury-perfil-email {
  margin: 3px 0 0 0;
  font-size: 0.9rem;
  color: var(--mercury-text-muted);
}

.mercury-perfil-tel {
  margin: 2px 0 0 0;
  font-size: 0.82rem;
  color: var(--mercury-text-hint);
}

.mercury-aviso-pendiente {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  background: rgba(245, 158, 11, 0.12);
  border: 1px solid rgba(245, 158, 11, 0.3);
  color: #fbbf24;
  padding: 12px 14px;
  border-radius: 12px;
  margin-top: 1rem;
  font-size: 0.88rem;
}

.mercury-aviso-pendiente ion-icon {
  font-size: 22px;
  flex-shrink: 0;
  margin-top: 2px;
}
</style>