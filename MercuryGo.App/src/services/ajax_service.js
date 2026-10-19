import { obtener_api_url } from '@/config/debug';
import { guardar_copia, obtener_copia } from '@/services/copia_local_service';
import { use_offline_store } from '@/stores/offline_store';
import {
  asegurar_token,
  cabecera_autorizacion,
  guardar_sesion,
  notificar_sesion_expirada,
  obtener_refresh_token
} from '@/services/token_service';

// Semáforo de renovación única: si varias peticiones reciben 401 simultáneamente,
// todas esperan la MISMA llamada a /api/sesion/refresh para evitar invalidar tokens por rotación.
let renovacion_en_curso = null;

function construir_url(endpoint) {
  const api_url = obtener_api_url();
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
  const timeout_ms = opciones.timeout || 15000;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeout_ms);

  // Asegura que el token esté cargado desde SecureStorage antes de construir la cabecera
  if (!endpoint.includes('/api/sesion/')) {
    await asegurar_token();
  }

  const cabeceras = {
    'Accept': opciones.es_blob ? '*/*' : 'application/json',
    ...cabecera_autorizacion(),
    ...opciones.headers
  };

  if (opciones.idempotency_key) {
    cabeceras['Idempotency-Key'] = String(opciones.idempotency_key);
  }

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
      const err401 = new Error('Sesión expirada. Por favor vuelva a iniciar sesión.');
      err401.estado_http = 401;
      err401.codigo = 'sesion_expirada';
      throw err401;
    }

    if (!respuesta.ok) {
      let mensaje = `Error del servidor (${respuesta.status})`;
      let codigo = 'error_servidor';
      try {
        const textoError = await respuesta.text();
        try {
          const datosError = JSON.parse(textoError);
          if (datosError && datosError.mensaje) mensaje = datosError.mensaje;
          if (datosError && datosError.codigo) codigo = datosError.codigo;
        } catch {
          if (textoError && textoError.length < 200) mensaje = textoError;
        }
      } catch {
        // Respuesta no leíble
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
    const resultado = await respuesta.json();
    if (opciones.guardable) {
      guardar_copia(endpoint, resultado);
    }
    try {
      use_offline_store().marcar_en_linea();
    } catch {}
    return resultado;
  } catch (error) {
    clearTimeout(timer);

    const esFalloRed = !error.estado_http || error.estado_http === 0;
    if (esFalloRed) {
      try {
        use_offline_store().marcar_desconectado();
      } catch {}
    }

    // Si la lectura es guardable y no hay conexión con la API, devolver la copia local si existe
    if (opciones.guardable && esFalloRed) {
      const copia = obtener_copia(endpoint);
      if (copia) {
        return copia;
      }
    }

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

// Descargas binarias (PDF / QR): XMLHttpRequest permite controlar el stream binario de forma nativa
// con el mismo token y la misma renovación automática ante un 401 que las llamadas JSON.
export function ajax_binario(configuracion = {}) {
  return ejecutar_binario(configuracion, true);
}

async function ejecutar_binario(configuracion, permitir_renovar) {
  const {
    endpoint,
    metodo = 'GET',
    timeout = 30000,
    headers = {}
  } = configuracion;

  await asegurar_token();

  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(metodo.toUpperCase(), construir_url(endpoint), true);
    xhr.responseType = 'blob';
    xhr.timeout = timeout;

    const cabeceras = {
      ...cabecera_autorizacion(),
      ...headers
    };

    Object.keys(cabeceras).forEach(nombre => xhr.setRequestHeader(nombre, cabeceras[nombre]));

    xhr.onload = async function () {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(xhr.response);
        return;
      }

      if (xhr.status === 401) {
        if (!permitir_renovar) {
          notificar_sesion_expirada();
          reject({
            estado_http: 401,
            codigo: 'sesion_expirada',
            mensaje: 'La sesión expiró. Volvé a iniciar sesión.'
          });
          return;
        }

        const renovado = await renovar_sesion();
        if (renovado) {
          ejecutar_binario(configuracion, false).then(resolve, reject);
          return;
        }

        notificar_sesion_expirada();
        reject({
          estado_http: 401,
          codigo: 'sesion_expirada',
          mensaje: 'La sesión expiró. Volvé a iniciar sesión.'
        });
        return;
      }

      let mensaje = `Error del servidor (${xhr.status})`;
      let codigo = 'error_servidor';
      try {
        if (/json/i.test(xhr.getResponseHeader('Content-Type') || '')) {
          const texto = await xhr.response?.text();
          const parsed = JSON.parse(texto);
          if (parsed?.mensaje) mensaje = parsed.mensaje;
          if (parsed?.codigo) codigo = parsed.codigo;
        }
      } catch {
        // Fallback
      }

      reject({
        estado_http: xhr.status,
        codigo,
        mensaje
      });
    };

    xhr.onerror = () => reject({
      estado_http: 0,
      codigo: 'sin_conexion',
      mensaje: 'No se pudo conectar con el servidor para descargar el archivo.'
    });

    xhr.ontimeout = () => reject({
      estado_http: 0,
      codigo: 'tiempo_espera_agotado',
      mensaje: 'Tiempo de espera agotado al descargar el archivo.'
    });

    xhr.send();
  });
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

  binario(configuracion = {}) {
    return ajax_binario(configuracion);
  },

  blob(endpoint, opciones = {}) {
    return ajax_binario({
      endpoint,
      metodo: 'GET',
      headers: opciones.headers,
      timeout: opciones.timeout
    });
  }
};

export function ajax_request(configuracion = {}) {
  const {
    endpoint,
    metodo = 'GET',
    datos = null,
    headers = {},
    timeout = 15000,
    guardable = false,
    renovar = true
  } = configuracion;
  return ejecutar_fetch(metodo.toUpperCase(), endpoint, datos, { headers, timeout, guardable, renovar });
}
