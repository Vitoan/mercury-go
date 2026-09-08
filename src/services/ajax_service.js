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
      mensaje: 'No se pudo conectar con la API. Revisá que el servidor esté levantado y que la URL sea la correcta.'
    };
  }
  return {
    estado_http: respuesta.status,
    codigo: 'error_servidor',
    mensaje: `Error del servidor (${respuesta.status})`
  };
}

async function enviar(metodo, endpoint, cuerpo = null, opciones = {}) {
  const url = construir_url(endpoint);
  const timeout_ms = opciones.timeout || 8000;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeout_ms);

  const cabeceras = {
    'Accept': 'application/json',
    ...opciones.headers
  };

  const config = {
    method: metodo,
    signal: controller.signal,
    headers: cabeceras,
    ...opciones
  };

  if (cuerpo !== null && cuerpo !== undefined && (metodo === 'POST' || metodo === 'PUT')) {
    cabeceras['Content-Type'] = 'application/json';
    config.body = JSON.stringify(cuerpo);
  }

  try {
    const respuesta = await fetch(url, config);
    clearTimeout(timer);

    if (!respuesta.ok) {
      let mensaje = `Error del servidor (${respuesta.status})`;
      let codigo = 'error_servidor';
      try {
        const datosError = await respuesta.json();
        if (datosError && datosError.mensaje) mensaje = datosError.mensaje;
        if (datosError && datosError.codigo) codigo = datosError.codigo;
      } catch {
        // Respuesta no JSON (ej: error 502/503 del gateway)
      }
      const error = new Error(mensaje);
      error.estado_http = respuesta.status;
      error.codigo = codigo;
      throw error;
    }

    // 204 No Content u operaciones sin cuerpo
    if (respuesta.status === 204) return null;

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

export const ajax_service = {
  get(endpoint, opciones = {}) {
    return enviar('GET', endpoint, null, opciones);
  },

  post(endpoint, cuerpo, opciones = {}) {
    return enviar('POST', endpoint, cuerpo, opciones);
  },

  put(endpoint, cuerpo, opciones = {}) {
    return enviar('PUT', endpoint, cuerpo, opciones);
  },

  delete(endpoint, opciones = {}) {
    return enviar('DELETE', endpoint, null, opciones);
  }
};
