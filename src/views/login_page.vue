<template>
  <ion-page class="mercury-login-page">
    <ion-content class="ion-padding mercury-login-content">
      <div class="mercury-login-container">
        <!-- Logo e Identidad Logística B2B -->
        <div class="mercury-login-header">
          <div class="mercury-logo-badge">
            <img src="/assets/logo.png" alt="MercuryGO Logo" class="mercury-logo-img" />
          </div>
          <h1 class="mercury-login-title">MercuryGO</h1>
          <p class="mercury-login-subtitle">Sistema de Distribución & Logística B2B</p>
          <span class="mercury-login-tag">Control de Acceso y Flota</span>
        </div>

        <!-- Formulario de Credenciales -->
        <ion-card class="mercury-login-card">
          <ion-card-content>
            <ion-item lines="inset" class="mercury-input-item">
              <ion-icon slot="start" :icon="mailOutline" class="mercury-input-icon" />
              <ion-input
                v-model="email"
                type="email"
                inputmode="email"
                autocomplete="username"
                label="Correo Corporativo"
                label-placement="stacked"
                placeholder="operador@mercurygo.local"
                @keyup.enter="entrar"
              />
            </ion-item>

            <ion-item lines="inset" class="mercury-input-item">
              <ion-icon slot="start" :icon="lockClosedOutline" class="mercury-input-icon" />
              <ion-input
                v-model="password"
                type="password"
                autocomplete="current-password"
                label="Contraseña"
                label-placement="stacked"
                placeholder="••••••••"
                @keyup.enter="entrar"
              />
            </ion-item>

            <div v-if="sesion_store.error" class="mercury-login-error">
              <ion-icon :icon="alertCircleOutline" />
              <span>{{ sesion_store.error }}</span>
            </div>

            <ion-button
              expand="block"
              class="mercury-btn-login"
              :disabled="sesion_store.cargando || !formularioValido"
              @click="entrar"
            >
              <ion-spinner v-if="sesion_store.cargando" name="crescent" />
              <span v-else>Iniciar Sesión</span>
            </ion-button>

            <!-- Botón de Huella Digital: solo si hay biometría y sesión previa guardada -->
            <ion-button
              v-if="puede_biometria"
              expand="block"
              fill="outline"
              class="mercury-btn-biometria"
              @click="entrar_con_biometria"
            >
              <ion-icon slot="start" :icon="fingerPrintOutline" />
              Desbloquear con Huella
            </ion-button>
          </ion-card-content>
        </ion-card>

        <!-- Accesos directos de demostración para cátedra -->
        <div class="mercury-login-demo">
          <p class="mercury-demo-title">Cuentas demo para evaluación:</p>
          <div class="mercury-demo-chips">
            <ion-chip class="mercury-demo-chip" @click="cargarCredenciales('admin@mercurygo.local')">
              <ion-label>ADMIN</ion-label>
            </ion-chip>
            <ion-chip class="mercury-demo-chip" @click="cargarCredenciales('operario@mercurygo.local')">
              <ion-label>OPERARIO</ion-label>
            </ion-chip>
            <ion-chip class="mercury-demo-chip" @click="cargarCredenciales('chofer@mercurygo.local')">
              <ion-label>CHOFER</ion-label>
            </ion-chip>
            <ion-chip class="mercury-demo-chip" @click="cargarCredenciales('cliente@mercurygo.local')">
              <ion-label>CLIENTE</ion-label>
            </ion-chip>
            <ion-chip class="mercury-demo-chip" @click="cargarCredenciales('nuevo@mercurygo.local')">
              <ion-label>SIN ROL</ion-label>
            </ion-chip>
          </div>
        </div>
      </div>
    </ion-content>
  </ion-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import {
  IonPage,
  IonContent,
  IonCard,
  IonCardContent,
  IonItem,
  IonInput,
  IonButton,
  IonSpinner,
  IonIcon,
  IonChip,
  IonLabel
} from '@ionic/vue';
import {
  mailOutline,
  lockClosedOutline,
  fingerPrintOutline,
  alertCircleOutline
} from 'ionicons/icons';
import { sesion_store } from '@/stores/sesion_store';
import { biometria_disponible, verificar_identidad } from '@/services/biometria_service';
import { restaurar_sesion } from '@/services/token_service';

const router = useRouter();

const email = ref('');
const password = ref('');
const puede_biometria = ref(false);

const formularioValido = computed(() => {
  return email.value.trim().length > 0 && password.value.length >= 6;
});

onMounted(async () => {
  const tokenGuardado = await restaurar_sesion();
  puede_biometria.value = Boolean(tokenGuardado) && (await biometria_disponible());
});

const entrar = async () => {
  if (!formularioValido.value) return;
  const ok = await sesion_store.entrar(email.value.trim(), password.value);
  if (ok) {
    router.replace('/app/inicio');
  }
};

const entrar_con_biometria = async () => {
  const confirmado = await verificar_identidad('Desbloquear sesión en MercuryGO');
  if (!confirmado) return;

  const usuario = await sesion_store.iniciar();
  if (usuario) {
    router.replace('/app/inicio');
  }
};

const cargarCredenciales = (correo) => {
  email.value = correo;
  password.value = 'Gestor123!';
};
</script>

<style scoped>
.mercury-login-page {
  --background: #090d16;
}

.mercury-login-content {
  display: flex;
  align-items: center;
  justify-content: center;
  --background: #090d16;
}

.mercury-login-container {
  max-width: 420px;
  margin: 2rem auto;
  padding: 0 1rem;
}

.mercury-login-header {
  text-align: center;
  margin-bottom: 2rem;
}

.mercury-logo-badge {
  width: 72px;
  height: 72px;
  margin: 0 auto 1.25rem;
  background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
  border: 1px solid rgba(56, 189, 248, 0.25);
  border-radius: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45);
  overflow: hidden;
  padding: 8px;
}

.mercury-logo-img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
}

.mercury-login-title {
  font-size: 2rem;
  font-weight: 800;
  letter-spacing: -0.025em;
  color: #f8fafc;
  margin: 0 0 0.25rem;
}

.mercury-login-subtitle {
  font-size: 0.95rem;
  color: #94a3b8;
  margin: 0 0 0.75rem;
}

.mercury-login-tag {
  display: inline-block;
  background: rgba(56, 189, 248, 0.12);
  color: #38bdf8;
  padding: 0.25rem 0.75rem;
  border-radius: 9999px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border: 1px solid rgba(56, 189, 248, 0.25);
}

.mercury-login-card {
  background: #0f172a;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 20px;
  box-shadow: 0 16px 32px rgba(0, 0, 0, 0.4);
}

.mercury-input-item {
  --background: #1e293b;
  --border-radius: 12px;
  margin-bottom: 1rem;
  --highlight-color-focused: #38bdf8;
}

.mercury-input-icon {
  color: #64748b;
  margin-right: 0.5rem;
}

.mercury-login-error {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  background: rgba(239, 68, 68, 0.12);
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.25);
  padding: 0.75rem 1rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.mercury-btn-login {
  --background: #0284c7;
  --background-activated: #0369a1;
  --border-radius: 12px;
  font-weight: 700;
  margin-top: 0.5rem;
  height: 48px;
}

.mercury-btn-biometria {
  --border-color: #38bdf8;
  --color: #38bdf8;
  --border-radius: 12px;
  font-weight: 600;
  margin-top: 0.75rem;
  height: 48px;
}

.mercury-login-demo {
  margin-top: 2rem;
  text-align: center;
}

.mercury-demo-title {
  font-size: 0.8rem;
  color: #64748b;
  margin-bottom: 0.5rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-weight: 600;
}

.mercury-demo-chips {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.25rem;
}

.mercury-demo-chip {
  --background: #1e293b;
  --color: #94a3b8;
  font-size: 0.75rem;
  font-weight: 700;
  cursor: pointer;
}
</style>
