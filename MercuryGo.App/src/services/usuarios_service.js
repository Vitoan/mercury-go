import { ajax_service } from './ajax_service';

export function obtener_usuarios(opciones = {}) {
  const params = new URLSearchParams();
  if (opciones.busqueda) params.set('busqueda', opciones.busqueda);
  if (opciones.pagina) params.set('pagina', opciones.pagina);
  if (opciones.tamano) params.set('tamano', opciones.tamano);

  const qs = params.toString();
  return ajax_service.get(`/api/usuarios${qs ? '?' + qs : ''}`);
}

export function crear_usuario(datos) {
  return ajax_service.post('/api/usuarios', datos);
}

export function cambiar_rol_usuario(id, rol_id) {
  return ajax_service.put(`/api/usuarios/${encodeURIComponent(id)}/rol`, { rol_id });
}

export function eliminar_usuario(id) {
  return ajax_service.delete(`/api/usuarios/${encodeURIComponent(id)}`);
}
