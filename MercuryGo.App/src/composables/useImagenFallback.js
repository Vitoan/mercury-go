import { ref } from 'vue';
import { resolver_url_imagen } from '@/utils/imagenes';

/**
 * Composable para resolución reactiva de URLs de imágenes y manejo de fallbacks.
 * Cuando una imagen de seed (/seed/productos/*) falla, redirige automáticamente
 * a los assets locales de la aplicación (/assets/productos/*).
 */
export function useImagenFallback() {
  const fallbacks = ref({});
  const errores = ref({});

  function obtenerSrc(id, imagen_url) {
    if (fallbacks.value[id]) {
      return fallbacks.value[id];
    }
    return resolver_url_imagen(imagen_url);
  }

  function marcarError(id, imagen_url) {
    if (imagen_url && imagen_url.startsWith('/seed/productos/') && !fallbacks.value[id]) {
      const nombre = imagen_url.split('/').pop();
      fallbacks.value[id] = `/assets/productos/${nombre}`;
      return;
    }
    errores.value[id] = true;
  }

  function limpiarError(id) {
    delete fallbacks.value[id];
    delete errores.value[id];
  }

  function tieneError(id) {
    return Boolean(errores.value[id]);
  }

  return {
    fallbacks,
    errores,
    obtenerSrc,
    marcarError,
    limpiarError,
    tieneError
  };
}
