import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import { cerrar_sesion, iniciar_sesion, obtener_mi_usuario } from '@/services/sesion_service';
import { usePedidosStore } from '@/stores/pedidos_store';
import { borrar_copias } from '@/services/copia_local_service';
import { use_offline_store } from '@/stores/offline_store';
import {
  al_expirar,
  borrar_sesion,
  guardar_sesion,
  obtener_refresh_token,
  restaurar_sesion
} from '@/services/token_service';

export const useSesionStore = defineStore('sesion', () => {
  const usuario = ref(null);
  const cargando = ref(false);
  const restaurando = ref(true);
  const error = ref(null);

  const autenticado = computed(() => Boolean(usuario.value));
  const rol_activo = computed(() => usuario.value?.rol_codigo || null);
  const pendiente_de_habilitacion = computed(() => Boolean(usuario.value) && !usuario.value?.rol_codigo);
  const es_admin = computed(() => usuario.value?.rol_codigo === 'ADMIN');

  async function preparar() {
    restaurando.value = true;
    try {
      return Boolean(await restaurar_sesion());
    } finally {
      restaurando.value = false;
    }
  }

  async function iniciar() {
    cargando.value = true;
    error.value = null;
    try {
      const token = await restaurar_sesion();
      if (!token) {
        usuario.value = null;
        return null;
      }
      const respuesta = await obtener_mi_usuario();
      usuario.value = respuesta?.usuario || null;
      return usuario.value;
    } catch (err) {
      const revocada = err?.estado_http === 401;
      if (revocada) await borrar_sesion();
      error.value = revocada
        ? 'La sesión guardada expiró. Ingresá con tu correo y contraseña.'
        : err?.mensaje || 'No se pudo reanudar la sesión.';
      usuario.value = null;
      return null;
    } finally {
      cargando.value = false;
    }
  }

  async function entrar(email, password) {
    cargando.value = true;
    error.value = null;
    try {
      const respuesta = await iniciar_sesion(email, password);
      const sesion = respuesta?.sesion || null;
      if (!sesion?.token) {
        error.value = 'No se pudo iniciar sesión.';
        return false;
      }
      await guardar_sesion(sesion.token, sesion.expira_en, sesion.refresh_token, sesion.refresh_expira_en);
      usuario.value = sesion.usuario;
      return true;
    } catch (err) {
      error.value = err.mensaje || 'Credenciales inválidas.';
      return false;
    } finally {
      cargando.value = false;
    }
  }

  async function salir(todos = false) {
    try {
      const refresh_token = await obtener_refresh_token();
      if (refresh_token) {
        await cerrar_sesion(refresh_token, todos);
      }
    } catch {
      // Ignorar fallo de red al cerrar sesión
    }
    await limpiar();
  }

  async function limpiar() {
    usuario.value = null;
    error.value = null;
    try {
      const pedidosStore = usePedidosStore();
      pedidosStore.resetear_store();
    } catch {
      // Ignorar si aún no está inicializado
    }
    await borrar_copias();
    try {
      const offlineStore = use_offline_store();
      offlineStore.guardar_cola([]);
    } catch {
      // Ignorar si aún no está inicializado
    }
    await borrar_sesion();
  }

  return {
    usuario,
    cargando,
    restaurando,
    error,
    autenticado,
    rol_activo,
    pendiente_de_habilitacion,
    es_admin,
    preparar,
    iniciar,
    entrar,
    salir,
    limpiar
  };
});

// Registrar callback para limpiar el estado si el token caduca
al_expirar(() => {
  try {
    const store = useSesionStore();
    store.limpiar();
  } catch {
    // Si pinia aún no está montado
  }
});

// Proxy retrocompatible
export const sesion_store = new Proxy({}, {
  get(_, prop) {
    const store = useSesionStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = useSesionStore();
    store[prop] = val;
    return true;
  }
});
