import { reactive, computed } from 'vue';
import {
  cambiar_rol_usuario,
  crear_usuario,
  eliminar_usuario,
  obtener_usuarios
} from '@/services/usuarios_service';

const state = reactive({
  usuarios: [],
  roles: [],
  pagina: null,
  filtros: { busqueda: '' },
  cargando: false,
  guardando: false,
  error: null
});

export const usuarios_store = {
  state,

  // Getters
  get usuarios() { return state.usuarios; },
  get roles() { return state.roles; },
  get cargando() { return state.cargando; },
  get guardando() { return state.guardando; },
  get error() { return state.error; },

  hay_usuarios: computed(() => state.usuarios.length > 0),
  hay_mas: computed(() => Boolean(state.pagina?.hay_mas)),
  total: computed(() => state.pagina?.total || 0),

  // Acciones
  async cargar(filtros = null) {
    state.cargando = true;
    state.error = null;
    if (filtros) state.filtros = { ...state.filtros, ...filtros };

    try {
      const respuesta = await obtener_usuarios({ ...state.filtros, pagina: 1 });
      state.usuarios = respuesta?.usuarios || [];
      state.roles = respuesta?.roles || [];
      state.pagina = respuesta?.pagina || null;
      return state.usuarios;
    } catch (error) {
      state.error = error.mensaje || 'No se pudieron cargar los usuarios.';
      state.usuarios = [];
      state.pagina = null;
      return [];
    } finally {
      state.cargando = false;
    }
  },

  async cargar_mas() {
    if (state.cargando || !this.hay_mas.value) return state.usuarios;
    state.cargando = true;

    try {
      const siguientePagina = (state.pagina?.pagina || 1) + 1;
      const respuesta = await obtener_usuarios({ ...state.filtros, pagina: siguientePagina });
      const conocidos = state.usuarios.map(u => u.id);
      const nuevos = (respuesta?.usuarios || []).filter(u => !conocidos.includes(u.id));
      state.usuarios = [...state.usuarios, ...nuevos];
      state.pagina = respuesta?.pagina || null;
      return state.usuarios;
    } catch (error) {
      state.error = error.mensaje || 'No se pudieron cargar más usuarios.';
      return state.usuarios;
    } finally {
      state.cargando = false;
    }
  },

  async crear(datos) {
    state.guardando = true;
    state.error = null;
    try {
      await crear_usuario(datos);
      await this.cargar();
      return true;
    } catch (error) {
      state.error = error.mensaje || 'No se pudo crear el usuario.';
      return false;
    } finally {
      state.guardando = false;
    }
  },

  async cambiar_rol(id, rol_id) {
    state.guardando = true;
    state.error = null;
    try {
      await cambiar_rol_usuario(id, rol_id);
      await this.cargar();
      return true;
    } catch (error) {
      state.error = error.mensaje || 'No se pudo actualizar el rol.';
      return false;
    } finally {
      state.guardando = false;
    }
  },

  async eliminar(id) {
    state.guardando = true;
    state.error = null;
    try {
      await eliminar_usuario(id);
      await this.cargar();
      return true;
    } catch (error) {
      state.error = error.mensaje || 'No se pudo dar de baja al usuario.';
      return false;
    } finally {
      state.guardando = false;
    }
  }
};
