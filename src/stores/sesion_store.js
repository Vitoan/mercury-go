import { reactive, computed } from 'vue';
import { cerrar_sesion, iniciar_sesion, obtener_mi_usuario } from '@/services/sesion_service';
import {
  al_expirar,
  borrar_sesion,
  guardar_sesion,
  obtener_refresh_token,
  restaurar_sesion
} from '@/services/token_service';

const state = reactive({
  usuario: null,
  cargando: false,
  restaurando: true,
  error: null
});

// Registrar callback para limpiar el estado si el token caduca de forma irrecuperable
al_expirar(() => {
  sesion_store.limpiar();
});

export const sesion_store = {
  // Estado reactivo
  state,

  // Getters computados
  get usuario() { return state.usuario; },
  get cargando() { return state.cargando; },
  get restaurando() { return state.restaurando; },
  get error() { return state.error; },

  autenticado: computed(() => Boolean(state.usuario)),
  rol_activo: computed(() => state.usuario?.rol_codigo || null),
  pendiente_de_habilitacion: computed(() => Boolean(state.usuario) && !state.usuario.rol_codigo),
  es_admin: computed(() => state.usuario?.rol_codigo === 'ADMIN'),

  // Acciones
  async iniciar() {
    state.restaurando = true;
    try {
      const token = await restaurar_sesion();
      if (!token) {
        state.usuario = null;
        return null;
      }
      const respuesta = await obtener_mi_usuario();
      state.usuario = respuesta?.usuario || null;
      return state.usuario;
    } catch {
      await borrar_sesion();
      state.usuario = null;
      return null;
    } finally {
      state.restaurando = false;
    }
  },

  async entrar(email, password) {
    state.cargando = true;
    state.error = null;
    try {
      const respuesta = await iniciar_sesion(email, password);
      const sesion = respuesta?.sesion || null;
      if (!sesion?.token) {
        state.error = 'No se pudo iniciar sesión.';
        return false;
      }
      await guardar_sesion(sesion.token, sesion.expira_en, sesion.refresh_token, sesion.refresh_expira_en);
      state.usuario = sesion.usuario;
      return true;
    } catch (error) {
      state.error = error.mensaje || 'Credenciales inválidas.';
      return false;
    } finally {
      state.cargando = false;
    }
  },

  async salir(todos = false) {
    try {
      const refresh_token = await obtener_refresh_token();
      if (refresh_token) {
        await cerrar_sesion(refresh_token, todos);
      }
    } catch {
      // Ignorar fallo de red al cerrar sesión
    }
    await this.limpiar();
  },

  async limpiar() {
    state.usuario = null;
    state.error = null;
    await borrar_sesion();
  }
};
