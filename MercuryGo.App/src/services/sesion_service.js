import { ajax_service } from './ajax_service';

export function iniciar_sesion(email, password) {
  return ajax_service.post('/api/sesion/login', { email, password });
}

export function renovar_sesion(refresh_token) {
  return ajax_service.post('/api/sesion/refresh', { refresh_token });
}

export function cerrar_sesion(refresh_token, todos = false) {
  return ajax_service.post('/api/sesion/logout', { refresh_token, todos });
}

// Obtiene los datos del usuario logueado (/api/sesion/yo)
export function obtener_mi_usuario() {
  return ajax_service.get('/api/sesion/yo');
}
