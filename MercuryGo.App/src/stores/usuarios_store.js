import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import {
  cambiar_rol_usuario,
  crear_usuario,
  eliminar_usuario,
  obtener_usuarios
} from '@/services/usuarios_service';

export const useUsuariosStore = defineStore('usuarios', () => {
  const usuarios = ref([]);
  const roles = ref([]);
  const pagina = ref(null);
  const filtros = ref({ busqueda: '' });
  const cargando = ref(false);
  const guardando = ref(false);
  const error = ref(null);

  const hay_usuarios = computed(() => usuarios.value.length > 0);
  const hay_mas = computed(() => Boolean(pagina.value?.hay_mas));
  const total = computed(() => pagina.value?.total || 0);

  async function cargar(nuevosFiltros = null) {
    cargando.value = true;
    error.value = null;
    if (nuevosFiltros) filtros.value = { ...filtros.value, ...nuevosFiltros };

    try {
      const respuesta = await obtener_usuarios({ ...filtros.value, pagina: 1 });
      usuarios.value = respuesta?.usuarios || [];
      roles.value = respuesta?.roles || [];
      pagina.value = respuesta?.pagina || null;
      return usuarios.value;
    } catch (err) {
      error.value = err.mensaje || 'No se pudieron cargar los usuarios.';
      usuarios.value = [];
      pagina.value = null;
      return [];
    } finally {
      cargando.value = false;
    }
  }

  async function cargar_mas() {
    if (cargando.value || !hay_mas.value) return usuarios.value;
    cargando.value = true;

    try {
      const siguientePagina = (pagina.value?.pagina || 1) + 1;
      const respuesta = await obtener_usuarios({ ...filtros.value, pagina: siguientePagina });
      const conocidos = usuarios.value.map(u => u.id);
      const nuevos = (respuesta?.usuarios || []).filter(u => !conocidos.includes(u.id));
      usuarios.value = [...usuarios.value, ...nuevos];
      pagina.value = respuesta?.pagina || null;
      return usuarios.value;
    } catch (err) {
      error.value = err.mensaje || 'No se pudieron cargar más usuarios.';
      return usuarios.value;
    } finally {
      cargando.value = false;
    }
  }

  async function crear(datos) {
    guardando.value = true;
    error.value = null;
    try {
      await crear_usuario(datos);
      await cargar();
      return true;
    } catch (err) {
      error.value = err.mensaje || 'No se pudo crear el usuario.';
      return false;
    } finally {
      guardando.value = false;
    }
  }

  async function cambiar_rol(id, rol_id) {
    guardando.value = true;
    error.value = null;
    try {
      await cambiar_rol_usuario(id, rol_id);
      await cargar();
      return true;
    } catch (err) {
      error.value = err.mensaje || 'No se pudo actualizar el rol.';
      return false;
    } finally {
      guardando.value = false;
    }
  }

  async function eliminar(id) {
    guardando.value = true;
    error.value = null;
    try {
      await eliminar_usuario(id);
      await cargar();
      return true;
    } catch (err) {
      error.value = err.mensaje || 'No se pudo dar de baja al usuario.';
      return false;
    } finally {
      guardando.value = false;
    }
  }

  return {
    usuarios,
    roles,
    pagina,
    filtros,
    cargando,
    guardando,
    error,
    hay_usuarios,
    hay_mas,
    total,
    cargar,
    cargar_mas,
    crear,
    cambiar_rol,
    eliminar
  };
});

// Proxy retrocompatible
export const usuarios_store = new Proxy({}, {
  get(_, prop) {
    const store = useUsuariosStore();
    return store[prop];
  },
  set(_, prop, val) {
    const store = useUsuariosStore();
    store[prop] = val;
    return true;
  }
});
