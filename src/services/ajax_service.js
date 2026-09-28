import { obtener_api_url } from '@/config/debug';
import {
  cabecera_autorizacion,
  guardar_sesion,
  notificar_sesion_expirada,
  obtener_refresh_token
} from '@/services/token_service';

// El único módulo de la app que habla con la API.
const api_url = obtener_api_url();

// Semáforo de renovación única: si varias peticiones reciben 401 simultáneamente,
// todas esperan la MISMA llamada a /api/sesion/refresh para evitar invalidar tokens por rotación.
let renovacion_en_curso = null;

function construir_url(endpoint) {
  if (!api_url) throw new Error('No se configuró la URL de la API.');
  return `${api_url.replace(/\/+$/, '')}/${String(endpoint).replace(/^\/+/, '')}`;
}

async function renovar_sesion() {
  if (renovacion_en_curso) return renovacion_en_curso;

  renovacion_en_curso = (async function () {
    const refresh_token = await obtener_refresh_token();
    if (!refresh_token) return false;

    try {
      const url = construir_url('/api/sesion/refresh');
      const respuesta = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json'
        },
        body: JSON.stringify({ refresh_token })
      });

      if (!respuesta.ok) return false;
      const datos = await respuesta.json();
      const sesion = datos?.sesion;
      if (!sesion?.token) return false;

      await guardar_sesion(sesion.token, sesion.expira_en, sesion.refresh_token, sesion.refresh_expira_en);
      return true;
    } catch {
      return false;
    }
  })().finally(() => {
    renovacion_en_curso = null;
  });

  return renovacion_en_curso;
}

function normalizar_error(respuesta) {
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

async function ejecutar_fetch(metodo, endpoint, cuerpo, opciones, reintentado = false) {
  const url = construir_url(endpoint);
  const timeout_ms = opciones.timeout || 10000;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeout_ms);

  const cabeceras = {
    'Accept': 'application/json',
    ...cabecera_autorizacion(),
    ...opciones.headers
  };

  const config = {
    method: metodo,
    signal: controller.signal,
    headers: cabeceras,
    ...opciones
  };

  const esFormData = cuerpo instanceof FormData;
  if (cuerpo !== null && cuerpo !== undefined && (metodo === 'POST' || metodo === 'PUT')) {
    if (esFormData) {
      // El navegador/webview arma el boundary multipart automáticamente: no fijar Content-Type
      config.body = cuerpo;
    } else {
      cabeceras['Content-Type'] = 'application/json';
      config.body = JSON.stringify(cuerpo);
    }
  }

  try {
    const respuesta = await fetch(url, config);
    clearTimeout(timer);

    // Si recibimos 401 Unauthorized y no es una ruta de sesión ni ya fue reintentada:
    if (respuesta.status === 401 && !endpoint.includes('/api/sesion/') && !reintentado) {
      const renovado = await renovar_sesion();
      if (renovado) {
        // Reintentamos la petición original con el nuevo token obtenido
        return await ejecutar_fetch(metodo, endpoint, cuerpo, opciones, true);
      }
      // Si la renovación falló, notificamos la expiración para redirigir al login
      notificar_sesion_expirada();
    }

    if (!respuesta.ok) {
      let mensaje = `Error del servidor (${respuesta.status})`;
      let codigo = 'error_servidor';
      try {
        const datosError = await respuesta.json();
        if (datosError && datosError.mensaje) mensaje = datosError.mensaje;
        if (datosError && datosError.codigo) codigo = datosError.codigo;
      } catch {
        // Respuesta no JSON
      }
      const error = new Error(mensaje);
      error.estado_http = respuesta.status;
      error.codigo = codigo;
      throw error;
    }

    if (respuesta.status === 204) return null;
    if (opciones.es_blob) {
      return await respuesta.blob();
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

export const ajax_service = {
  get(endpoint, opciones = {}) {
    return ejecutar_fetch('GET', endpoint, null, opciones);
  },

  post(endpoint, cuerpo, opciones = {}) {
    return ejecutar_fetch('POST', endpoint, cuerpo, opciones);
  },

  put(endpoint, cuerpo, opciones = {}) {
    return ejecutar_fetch('PUT', endpoint, cuerpo, opciones);
  },

  delete(endpoint, opciones = {}) {
    return ejecutar_fetch('DELETE', endpoint, null, opciones);
  },

  blob(endpoint, opciones = {}) {
    return ejecutar_fetch('GET', endpoint, null, {
      ...opciones,
      es_blob: true,
      headers: {
        'Accept': '*/*',
        ...(opciones.headers || {})
      }
    });
  }
};
