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
    const claveExacta = prefijo + endpoint;
    const copia = localStorage.getItem(claveExacta);
    if (copia) return JSON.parse(copia);

    // Fallback: probar con/sin barra inicial
    const endpointAlt = endpoint.startsWith('/') ? endpoint.slice(1) : `/${endpoint}`;
    const copiaAlt = localStorage.getItem(prefijo + endpointAlt);
    if (copiaAlt) return JSON.parse(copiaAlt);

    // Fallback para pedidos: si no hay coincidencia exacta de query string, devolver cualquier copia guardada de pedidos
    if (endpoint.includes('api/pedidos') && !endpoint.includes('/resumen')) {
      const clavePedidos = Object.keys(localStorage).find(
        (k) => k.startsWith(prefijo) && k.includes('api/pedidos') && !k.includes('/resumen')
      );
      if (clavePedidos) {
        const fallback = localStorage.getItem(clavePedidos);
        return fallback ? JSON.parse(fallback) : null;
      }
    }

    // Fallback para productos: si no hay coincidencia exacta de query string, devolver copia del listado de catálogo
    if (endpoint.includes('api/productos/listado')) {
      const claveProductos = Object.keys(localStorage).find(
        (k) => k.startsWith(prefijo) && k.includes('api/productos/listado')
      );
      if (claveProductos) {
        const fallback = localStorage.getItem(claveProductos);
        return fallback ? JSON.parse(fallback) : null;
      }
    }
    return null;
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
