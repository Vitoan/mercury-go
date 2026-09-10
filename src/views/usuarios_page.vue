<template>
  <comp-page titulo="Gestión de Usuarios" :mostrar_actualizar="true" @actualizar="cargar">
    <div class="ion-padding mercury-usuarios-stack">
      <!-- Buscador por nombre o email con debounce -->
      <comp-buscador
        v-model="busqueda"
        placeholder="Buscar por nombre, email o teléfono…"
        @buscar="buscar"
      />

      <!-- Botón para nuevo usuario -->
      <ion-button expand="block" class="mercury-btn-nuevo" @click="abrirNuevo">
        <ion-icon slot="start" :icon="personAddOutline" />
        Nuevo Operador / Usuario
      </ion-button>

      <!-- Estado: Cargando primera vez -->
      <comp-esqueleto v-if="usuarios_store.cargando && !usuarios_store.hay_usuarios.value" />

      <!-- Estado: Error al conectar -->
      <comp-estado-error
        v-else-if="usuarios_store.error && !usuarios_store.hay_usuarios.value"
        :mensaje="usuarios_store.error"
        @reintentar="cargar"
      />

      <!-- Estado: Sin resultados -->
      <comp-estado-vacio
        v-else-if="!usuarios_store.hay_usuarios.value"
        mensaje="No se encontraron usuarios registrados con ese filtro."
      />

      <!-- Lista paginada de usuarios -->
      <comp-lista
        v-else
        :cargando="usuarios_store.cargando"
        :hay_mas="usuarios_store.hay_mas.value"
        :mostrados="usuarios_store.usuarios.length"
        :total="usuarios_store.total.value"
        @cargar_mas="usuarios_store.cargar_mas"
      >
        <ion-item
          v-for="u in usuarios_store.usuarios"
          :key="u.id"
          class="mercury-usuario-item"
          button
          @click="abrirAcciones(u)"
        >
          <ion-avatar slot="start" class="mercury-avatar-inicial">
            <span>{{ inicial(u.nombre) }}</span>
          </ion-avatar>

          <ion-label>
            <div class="mercury-usuario-cabecera">
              <h3>{{ u.nombre }}</h3>
              <ion-badge :color="obtenerColorBadge(u.rol_codigo)" class="mercury-badge-rol">
                {{ u.rol_nombre || 'Sin Habilitar' }}
              </ion-badge>
            </div>
            <p>{{ u.email }}</p>
            <p v-if="u.telefono" class="mercury-usuario-tel">📞 {{ u.telefono }}</p>
          </ion-label>
        </ion-item>
      </comp-lista>
    </div>

    <!-- Modal de Alta de Usuario -->
    <ion-modal :is-open="modalAbierto" @didDismiss="modalAbierto = false">
      <ion-header>
        <ion-toolbar>
          <ion-title>Alta de Usuario</ion-title>
          <ion-buttons slot="end">
            <ion-button @click="modalAbierto = false">Cerrar</ion-button>
          </ion-buttons>
        </ion-toolbar>
      </ion-header>
      <ion-content class="ion-padding">
        <ion-card class="mercury-card-modal">
          <ion-card-content>
            <ion-item lines="inset">
              <ion-input
                v-model="formulario.nombre"
                label="Nombre y Apellido"
                label-placement="stacked"
                placeholder="Ej: Marcelo Gómez"
              />
            </ion-item>

            <ion-item lines="inset">
              <ion-input
                v-model="formulario.email"
                type="email"
                label="Email Corporativo"
                label-placement="stacked"
                placeholder="mgomez@mercurygo.local"
              />
            </ion-item>

            <ion-item lines="inset">
              <ion-input
                v-model="formulario.telefono"
                type="tel"
                label="Teléfono"
                label-placement="stacked"
                placeholder="266-4112233"
              />
            </ion-item>

            <ion-item lines="inset">
              <ion-input
                v-model="formulario.password"
                type="password"
                label="Contraseña Temporal"
                label-placement="stacked"
                placeholder="Mínimo 8 caracteres"
              />
            </ion-item>

            <ion-item lines="none">
              <ion-select
                v-model="formulario.rol_id"
                label="Rol de Acceso Logístico"
                label-placement="stacked"
                interface="popover"
                placeholder="Seleccionar rol..."
              >
                <ion-select-option :value="null">Sin rol (queda en espera de habilitación)</ion-select-option>
                <ion-select-option v-for="r in usuarios_store.roles" :key="r.id" :value="r.id">
                  {{ r.nombre }} ({{ r.codigo }})
                </ion-select-option>
              </ion-select>
            </ion-item>

            <div v-if="errorModal" class="mercury-modal-error">
              <span>{{ errorModal }}</span>
            </div>

            <ion-button
              expand="block"
              style="margin-top: 1rem;"
              :disabled="usuarios_store.guardando"
              @click="guardarNuevo"
            >
              <ion-spinner v-if="usuarios_store.guardando" name="crescent" />
              <span v-else>Guardar Usuario</span>
            </ion-button>
          </ion-card-content>
        </ion-card>
      </ion-content>
    </ion-modal>

    <!-- Action Sheet para Acciones sobre Usuario -->
    <ion-action-sheet
      :is-open="sheetAbierto"
      :header="`Operador: ${usuarioSeleccionado?.nombre || ''}`"
      :buttons="botonesAcciones"
      @didDismiss="sheetAbierto = false"
    />
  </comp-page>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonToolbar,
  IonTitle,
  IonItem,
  IonAvatar,
  IonLabel,
  IonBadge,
  IonModal,
  IonCard,
  IonCardContent,
  IonInput,
  IonSelect,
  IonSelectOption,
  IonIcon,
  IonSpinner,
  IonActionSheet
} from '@ionic/vue';
import { personAddOutline } from 'ionicons/icons';
import CompPage from '@/components/estructura/comp_page.vue';
import CompBuscador from '@/components/base/comp_buscador.vue';
import CompLista from '@/components/base/comp_lista.vue';
import CompEsqueleto from '@/components/base/comp_esqueleto.vue';
import CompEstadoError from '@/components/base/comp_estado_error.vue';
import CompEstadoVacio from '@/components/base/comp_estado_vacio.vue';
import { usuarios_store } from '@/stores/usuarios_store';

const busqueda = ref('');
const modalAbierto = ref(false);
const sheetAbierto = ref(false);
const usuarioSeleccionado = ref(null);
const errorModal = ref('');
const botonesAcciones = ref([]);

const formulario = ref({
  nombre: '',
  email: '',
  telefono: '',
  password: '',
  rol_id: null
});

onMounted(() => {
  usuarios_store.cargar();
});

const cargar = () => {
  usuarios_store.cargar();
};

const buscar = (texto) => {
  usuarios_store.cargar({ busqueda: texto });
};

const inicial = (nombre) => {
  return (nombre || 'U').charAt(0).toUpperCase();
};

const obtenerColorBadge = (codigo) => {
  switch (codigo) {
    case 'ADMIN': return 'primary';
    case 'OPERARIO': return 'warning';
    case 'CHOFER': return 'tertiary';
    case 'CLIENTE': return 'success';
    default: return 'medium';
  }
};

const abrirNuevo = () => {
  errorModal.value = '';
  formulario.value = {
    nombre: '',
    email: '',
    telefono: '',
    password: '',
    rol_id: null
  };
  modalAbierto.value = true;
};

const guardarNuevo = async () => {
  errorModal.value = '';
  if (!formulario.value.nombre.trim()) {
    errorModal.value = 'El nombre es obligatorio.';
    return;
  }
  if (!formulario.value.email.includes('@')) {
    errorModal.value = 'Email inválido.';
    return;
  }
  if (formulario.value.password.length < 8) {
    errorModal.value = 'La contraseña requiere al menos 8 caracteres.';
    return;
  }

  const exito = await usuarios_store.crear(formulario.value);
  if (exito) {
    modalAbierto.value = false;
  } else {
    errorModal.value = usuarios_store.error || 'Error al guardar usuario.';
  }
};

const abrirAcciones = (u) => {
  usuarioSeleccionado.value = u;
  const botones = [];

  // Opciones de cambio de rol
  usuarios_store.roles.forEach(rol => {
    if (rol.id !== u.rol_id) {
      botones.push({
        text: `Asignar Rol: ${rol.nombre}`,
        handler: async () => {
          await usuarios_store.cambiar_rol(u.id, rol.id);
        }
      });
    }
  });

  if (u.rol_id !== null) {
    botones.push({
      text: 'Quitar Rol (dejar Sin Habilitar)',
      handler: async () => {
        await usuarios_store.cambiar_rol(u.id, null);
      }
    });
  }

  // Opción de baja lógica
  botones.push({
    text: 'Dar de Baja Usuario',
    role: 'destructive',
    handler: async () => {
      await usuarios_store.eliminar(u.id);
    }
  });

  botones.push({ text: 'Cancelar', role: 'cancel' });
  botonesAcciones.value = botones;
  sheetAbierto.value = true;
};
</script>

<style scoped>
.mercury-usuarios-stack {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mercury-btn-nuevo {
  --background: #0284c7;
  --border-radius: 12px;
  font-weight: 700;
  margin-top: 4px;
}

.mercury-usuario-item {
  --background: #0f172a;
  --border-radius: 12px;
  margin-bottom: 8px;
}

.mercury-avatar-inicial {
  background: #1e293b;
  color: #38bdf8;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 1.1rem;
}

.mercury-usuario-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.mercury-usuario-cabecera h3 {
  font-size: 1rem;
  font-weight: 700;
  margin: 0;
}

.mercury-badge-rol {
  font-size: 0.72rem;
  padding: 4px 8px;
  border-radius: 6px;
}

.mercury-usuario-tel {
  font-size: 0.8rem;
  color: #94a3b8;
}

.mercury-card-modal {
  background: #0f172a;
  border-radius: 16px;
}

.mercury-modal-error {
  background: rgba(239, 68, 68, 0.12);
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.25);
  padding: 8px 12px;
  border-radius: 8px;
  margin-top: 10px;
  font-size: 0.85rem;
}
</style>
