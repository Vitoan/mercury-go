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

    <!-- Modal de Alta de Usuario Modular -->
    <comp-usuario-modal
      :abierto="modalAbierto"
      @cerrar="modalAbierto = false"
      @usuario_creado="alCrearUsuario"
    />

    <!-- Toast de notificaciones y feedback -->
    <ion-toast
      :is-open="toast.visible.value"
      :message="toast.mensaje.value"
      :color="toast.color.value"
      duration="3000"
      @didDismiss="toast.ocultar"
    />

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
import { ref } from 'vue';
import {
  IonButton,
  IonItem,
  IonAvatar,
  IonLabel,
  IonBadge,
  IonIcon,
  IonActionSheet,
  IonToast,
  onIonViewWillEnter
} from '@ionic/vue';
import { personAddOutline } from 'ionicons/icons';
import CompPage from '@/components/estructura/comp_page.vue';
import CompBuscador from '@/components/base/comp_buscador.vue';
import CompLista from '@/components/base/comp_lista.vue';
import CompEsqueleto from '@/components/base/comp_esqueleto.vue';
import CompEstadoError from '@/components/base/comp_estado_error.vue';
import CompEstadoVacio from '@/components/base/comp_estado_vacio.vue';
import CompUsuarioModal from '@/components/dominio/usuarios/comp_usuario_modal.vue';
import { usuarios_store } from '@/stores/usuarios_store';
import { vibrar_toque, vibrar_exito, vibrar_error } from '@/services/vibracion_service';
import { useToast } from '@/composables/useToast';

const toast = useToast();
const busqueda = ref('');
const modalAbierto = ref(false);
const sheetAbierto = ref(false);
const usuarioSeleccionado = ref(null);
const botonesAcciones = ref([]);

onIonViewWillEnter(() => {
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
  vibrar_toque();
  modalAbierto.value = true;
};

const alCrearUsuario = (nuevo) => {
  toast.mostrarExito(`¡Operador '${nuevo.nombre}' creado exitosamente!`);
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
          try {
            await usuarios_store.cambiar_rol(u.id, rol.id);
            await vibrar_exito();
            toast.mostrarExito(`Rol '${rol.nombre}' asignado a ${u.nombre}.`);
          } catch (err) {
            await vibrar_error();
            toast.mostrarError(err.message || 'Error al cambiar rol.');
          }
        }
      });
    }
  });

  if (u.rol_id !== null) {
    botones.push({
      text: 'Quitar Rol (dejar Sin Habilitar)',
      handler: async () => {
        try {
          await usuarios_store.cambiar_rol(u.id, null);
          await vibrar_toque();
          toast.mostrarInfo(`Rol revocado para ${u.nombre}.`);
        } catch (err) {
          await vibrar_error();
          toast.mostrarError(err.message || 'Error al revocar rol.');
        }
      }
    });
  }

  // Opción de baja lógica
  botones.push({
    text: 'Dar de Baja Usuario',
    role: 'destructive',
    handler: async () => {
      try {
        await usuarios_store.eliminar(u.id);
        await vibrar_exito();
        toast.mostrarExito(`Usuario '${u.nombre}' dado de baja.`);
      } catch (err) {
        await vibrar_error();
        toast.mostrarError(err.message || 'Error al dar de baja.');
      }
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
  --background: var(--mercury-bg-card);
  --border-radius: 12px;
  margin-bottom: 8px;
  border: 1px solid var(--mercury-border-subtle);
}

.mercury-avatar-inicial {
  background: var(--mercury-bg-subtle);
  color: var(--mercury-accent);
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
  color: var(--mercury-text-title);
}

.mercury-badge-rol {
  font-size: 0.72rem;
  padding: 4px 8px;
  border-radius: 6px;
}

.mercury-usuario-tel {
  font-size: 0.8rem;
  color: var(--mercury-text-muted);
}

.mercury-card-modal {
  background: var(--mercury-bg-card);
  border-radius: 16px;
  border: 1px solid var(--mercury-border-subtle);
}

.mercury-modal-error {
  background: rgba(239, 68, 68, 0.12);
  color: var(--ion-color-danger, #ef4444);
  border: 1px solid rgba(239, 68, 68, 0.25);
  padding: 8px 12px;
  border-radius: 8px;
  margin-top: 10px;
  font-size: 0.85rem;
}
</style>
