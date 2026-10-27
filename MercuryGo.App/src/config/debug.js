import { Capacitor } from '@capacitor/core';

const api_url_remota = 'http://mercurygo.runasp.net';

export const debug_config = {
  debug_activado: import.meta.env.VITE_DEBUG_ACTIVADO !== 'false',
  api_url_debug: (import.meta.env.VITE_API_URL_DEBUG || api_url_remota).replace(/\/+$/, '')
};

export function obtener_api_url() {
  // En navegador web durante desarrollo local, usar '' para aprovechar el proxy de Vite y evitar CORS
  if (!Capacitor.isNativePlatform() && import.meta.env.DEV) {
    return '';
  }

  const api_url_entorno = (import.meta.env.VITE_API_URL || api_url_remota).replace(/\/+$/, '');
  const url_final = debug_config.debug_activado ? debug_config.api_url_debug : api_url_entorno;
  return url_final;
}

if (typeof window !== 'undefined') {
  const urlDestino = obtener_api_url() || '(Proxy local Vite -> ' + api_url_remota + ')';
  console.info('%c[MercuryGO Config] Conectado a API:%c ' + urlDestino, 'background: #2563eb; color: #fff; padding: 2px 6px; border-radius: 4px;', 'color: #2563eb; font-weight: bold;');
}

export { resolver_url_imagen } from '@/utils/imagenes';
