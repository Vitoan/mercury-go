import { obtener_api_url } from '@/config/debug';

// El único módulo de la app que habla con la API. El camino es siempre
// página -> store o service -> ajax_service -> API.
const api_url = obtener_api_url();

function construir_url(endpoint) {
  if (!api_url) throw new Error('No se configuró la URL de la API.');
  return `${api_url.replace(/\/+$/, '')}/${String(endpoint).replace(/^\/+/, '')}`;
}

function normalizar_error(respuesta, error_nativo) {
  if (!respuesta) {
    return {
      estado_http: 0,
      codigo: 'sin_conexion',
      mensaje: 'No se pudo conectar con la API. Revisá que esté levantada y que la URL sea la correcta.'
    };
  }
  return {
    estado_http: respuesta.status,
    codigo: 'error_servidor',
    mensaje: `Error del servidor (${respuesta.status})`
  };
}

export const ajax_service = {
  async get(endpoint, opciones = {}) {
    const url = construir_url(endpoint);
    const timeout_ms = opciones.timeout || 5000;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeout_ms);

    try {
      const respuesta = await fetch(url, {
        method: 'GET',
        signal: controller.signal,
        headers: {
          'Accept': 'application/json',
          ...opciones.headers
        },
        ...opciones
      });

      clearTimeout(timer);

      if (!respuesta.ok) {
        let mensaje = `Error del servidor (${respuesta.status})`;
        let codigo = 'error_servidor';
        try {
          const cuerpo = await respuesta.json();
          if (cuerpo && cuerpo.mensaje) mensaje = cuerpo.mensaje;
          if (cuerpo && cuerpo.codigo) codigo = cuerpo.codigo;
        } catch {
          // Ignorar error al parsear JSON
        }
        const error = new Error(mensaje);
        error.estado_http = respuesta.status;
        error.codigo = codigo;
        throw error;
      }

      return await respuesta.json();
    } catch (error) {
      clearTimeout(timer);

      if (error.name === 'AbortError') {
        const err = new Error('Tiempo de espera agotado. La API no responde.');
        err.estado_http = 0;
        err.codigo = 'tiempo_espera_agotado';
        throw err;
      }

      if (error.estado_http) throw error;
      const normalizado = normalizar_error(null, error);
      const err = new Error(normalizado.mensaje);
      err.estado_http = 0;
      err.codigo = normalizado.codigo;
      throw err;
    }
  }
};
