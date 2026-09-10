<template>
  <comp-page titulo="Mi cuenta">
    <div class="cuenta-stack">
      <!-- Tarjeta de Usuario y Rol -->
      <ion-card class="mercury-card-perfil">
        <ion-card-header>
          <ion-card-subtitle>Sesión Activa</ion-card-subtitle>
          <ion-card-title>{{ sesion_store.usuario?.nombre || 'Usuario Conectado' }}</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none" class="mercury-item-perfil">
            <ion-icon slot="start" :icon="personCircleOutline" class="mercury-avatar-icon" />
            <ion-label>
              <h3>{{ sesion_store.usuario?.email || 'Sin correo' }}</h3>
              <p v-if="sesion_store.usuario?.telefono">📞 {{ sesion_store.usuario.telefono }}</p>
            </ion-label>
            <ion-badge
              slot="end"
              :color="badgeColor"
              class="mercury-badge-rol"
            >
              {{ sesion_store.usuario?.rol_nombre || 'Sin Habilitar' }}
            </ion-badge>
          </ion-item>

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

      <!-- Tarjeta de Apariencia -->
      <ion-card>
        <ion-card-header>
          <ion-card-subtitle>Apariencia</ion-card-subtitle>
          <ion-card-title>Tema de la app</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none">
            <ion-icon slot="start" :icon="contrastOutline" />
            <ion-label>Modo oscuro</ion-label>
            <ion-toggle slot="end" :checked="modoOscuro" @ionChange="toggleTema" />
          </ion-item>
        </ion-card-content>
      </ion-card>

      <!-- Tarjeta de Diagnóstico de Red y API -->
      <ion-card>
        <ion-card-header>
          <ion-card-subtitle>Conexión</ion-card-subtitle>
          <ion-card-title>Diagnóstico de Red</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none">
            <ion-icon slot="start" :icon="cloudOutline" />
            <ion-label>
              <p>Esta app le pide los datos a</p>
              <h3 style="font-weight: 600; color: #38bdf8;">{{ api_url }}</h3>
            </ion-label>
          </ion-item>

          <div v-if="estado_diagnostico !== 'inicial'" class="diagnostico-resultado" :class="estado_diagnostico">
            <ion-spinner v-if="estado_diagnostico === 'probando'" name="dots" />
            <ion-icon v-else :icon="estado_diagnostico === 'ok' ? checkmarkCircleOutline : alertCircleOutline" />
            <span>{{ mensaje_diagnostico }}</span>
          </div>

          <ion-button
            expand="block"
            fill="outline"
            style="margin-top: 14px;"
            :disabled="estado_diagnostico === 'probando'"
            @click="probar_conexion"
          >
            Probar conexión
          </ion-button>
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
  personCircleOutline, contrastOutline, cloudOutline, 
  checkmarkCircleOutline, alertCircleOutline, logOutOutline 
} from 'ionicons/icons';
import CompPage from '../components/estructura/comp_page.vue';
import { alternar_tema, es_tema_oscuro } from '../config/tema';
import { obtener_api_url } from '../config/debug';
import { consultar_health } from '../services/health_service';
import { sesion_store } from '@/stores/sesion_store';

const router = useRouter();

const modoOscuro = ref(false);
const api_url = ref(obtener_api_url());
const estado_diagnostico = ref('inicial');
const mensaje_diagnostico = ref('');

const badgeColor = computed(() => {
  const rol = sesion_store.rol_activo.value;
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

const probar_conexion = async () => {
  estado_diagnostico.value = 'probando';
  mensaje_diagnostico.value = 'Probando conexión con la API…';
  try {
    const res = await consultar_health();
    estado_diagnostico.value = 'ok';
    const hora = res.utc ? new Date(res.utc).toLocaleTimeString('es-AR') : '';
    mensaje_diagnostico.value = `La API respondió: ${res.status || 'ok'} ${hora ? '· ' + hora : ''}`;
  } catch (error) {
    estado_diagnostico.value = 'error';
    mensaje_diagnostico.value = error.message || 'No se pudo conectar con la API.';
  }
};

const confirmarCerrarSesion = async () => {
  await sesion_store.salir();
  router.replace('/login');
};

onMounted(() => {
  modoOscuro.value = es_tema_oscuro();
});
</script>

<style scoped>
.cuenta-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mercury-avatar-icon {
  font-size: 38px;
  color: #38bdf8;
}

.mercury-badge-rol {
  font-size: 0.8rem;
  padding: 6px 12px;
  font-weight: 700;
  border-radius: 9999px;
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

.diagnostico-resultado {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 8px;
  margin-top: 10px;
  font-size: 0.9rem;
}

.diagnostico-resultado.ok {
  background: rgba(34, 197, 94, 0.12);
  color: var(--ion-color-success, #22c55e);
}

.diagnostico-resultado.error {
  background: rgba(239, 68, 68, 0.12);
  color: var(--ion-color-danger, #ef4444);
}

.diagnostico-resultado.probando {
  background: rgba(56, 189, 248, 0.12);
  color: var(--ion-color-primary, #38bdf8);
}
</style>