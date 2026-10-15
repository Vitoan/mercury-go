// La copia local: la última respuesta que dio la API a cada lectura marcada como `guardable`.
// Sin red, ajax_service devuelve esta copia y la pantalla muestra lo último que se vio.
const prefijo = 'mercurygo_copia:';

export function guardar_copia(endpoint, respuesta) {
  try {
    localStorage.setItem(prefijo + endpoint, JSON.stringify(respuesta));
  } catch (error) {
    console.warn('No se pudo guardar copia local en localStorage:', error);
  }
}

export function obtener_copia(endpoint) {
  try {
    const copia = localStorage.getItem(prefijo + endpoint);
    return copia ? JSON.parse(copia) : null;
  } catch {
    return null;
  }
}

// Al cerrar la sesión no queda nada: las copias son de quien las cargó.
export function borrar_copias() {
  try {
    Object.keys(localStorage)
      .filter((clave) => clave.startsWith(prefijo))
      .forEach((clave) => localStorage.removeItem(clave));
  } catch (error) {
    console.warn('Error al borrar copias locales:', error);
  }
}
