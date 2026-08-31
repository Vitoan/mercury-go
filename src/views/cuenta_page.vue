<template>
  <comp-page titulo="Mi cuenta">
    <div class="cuenta-stack">
      <!-- Tarjeta de Usuario -->
      <ion-card>
        <ion-card-header>
          <ion-card-subtitle>Perfil</ion-card-subtitle>
          <ion-card-title>Victor Angel Aguilera Ocampo</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none">
            <ion-icon slot="start" :icon="personCircleOutline"></ion-icon>
            <ion-label>
              <h3>Alumno TUDS 2026</h3>
              <p>Desarrollador MercuryGO</p>
            </ion-label>
          </ion-item>
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
            <ion-icon slot="start" :icon="contrastOutline"></ion-icon>
            <ion-label>Modo oscuro</ion-label>
            <ion-toggle slot="end" :checked="modoOscuro" @ionChange="toggleTema"></ion-toggle>
          </ion-item>
        </ion-card-content>
      </ion-card>

      <!-- Tarjeta de Diagnóstico de Red y API (Punto 12 de la Unidad 3) -->
      <ion-card>
        <ion-card-header>
          <ion-card-subtitle>Conexión</ion-card-subtitle>
          <ion-card-title>Diagnóstico</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item lines="none">
            <ion-icon slot="start" :icon="cloudOutline"></ion-icon>
            <ion-label>
              <p>Esta app le pide los datos a</p>
              <h3 style="font-weight: 600; color: #38BDF8;">{{ api_url }}</h3>
            </ion-label>
          </ion-item>

          <div v-if="estado_diagnostico !== 'inicial'" class="diagnostico-resultado" :class="estado_diagnostico">
            <ion-spinner v-if="estado_diagnostico === 'probando'" name="dots"></ion-spinner>
            <ion-icon v-else :icon="estado_diagnostico === 'ok' ? checkmarkCircleOutline : alertCircleOutline"></ion-icon>
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
import { ref, onMounted } from 'vue';
import { 
  IonCard, IonCardHeader, IonCardSubtitle, IonCardTitle, IonCardContent,
  IonItem, IonLabel, IonIcon, IonToggle, IonButton, IonSpinner 
} from '@ionic/vue';
import { 
  personCircleOutline, contrastOutline, cloudOutline, 
  checkmarkCircleOutline, alertCircleOutline 
} from 'ionicons/icons';
import CompPage from '../components/estructura/comp_page.vue';
import { alternar_tema, es_tema_oscuro } from '../config/tema';
import { obtener_api_url } from '../config/debug';
import { consultar_health } from '../services/health_service';

const modoOscuro = ref(false);
const api_url = ref(obtener_api_url());
const estado_diagnostico = ref('inicial');
const mensaje_diagnostico = ref('');

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