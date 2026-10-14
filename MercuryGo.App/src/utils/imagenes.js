import { obtener_api_url } from '@/config/debug';

// La API guarda rutas relativas (`/uploads/productos/guid.ext`), no URLs completas.
// Resolver aquí permite que cambiar la IP de la máquina o de red local
// no invalide las fotos mostradas en la app móvil.
export function resolver_url_imagen(url, imagen_default = '') {
  if (!url) return imagen_default;
  if (
    url.startsWith('http://') ||
    url.startsWith('https://') ||
    url.startsWith('data:') ||
    url.startsWith('blob:')
  ) {
    return url;
  }
  const baseUrl = obtener_api_url();
  if (url.startsWith('/')) {
    return `${baseUrl}${url}`;
  }
  return `${baseUrl}/${url}`;
}
