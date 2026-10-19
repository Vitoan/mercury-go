import { SecureStorage } from '@aparajita/capacitor-secure-storage';

// Dónde vive la sesión en el dispositivo. Es el único módulo que la lee y escribe.
// En el teléfono usa el almacenamiento seguro del sistema (Keystore en Android), no localStorage:
// un token en localStorage puede ser leído por scripts inyectados en la WebView.
// En el navegador el plugin cae a localStorage para permitir desarrollo fluido.
const CLAVE_TOKEN = 'mercurygo_token';
const CLAVE_REFRESH = 'mercurygo_refresh';
const CLAVE_EXPIRA = 'mercurygo_expira';

let token_en_memoria = null;
let al_expirar_sesion = null;

async function guardar(clave, valor) {
  if (valor === null || valor === undefined) {
    await SecureStorage.remove(clave);
    return;
  }
  await SecureStorage.set(clave, String(valor));
}

async function leer(clave) {
  try {
    return await SecureStorage.get(clave);
  } catch {
    // Si la clave no existe, no es un error de fallo; simplemente no hay sesión previa
    return null;
  }
}

async function migrar_claves_inseguras_si_existen() {
  try {
    // Si hubiese quedado algún token residual en localStorage de versiones previas
    const tokenViejo = typeof window !== 'undefined' ? window.localStorage?.getItem(CLAVE_TOKEN) : null;
    const refreshViejo = typeof window !== 'undefined' ? window.localStorage?.getItem(CLAVE_REFRESH) : null;
    const expiraViejo = typeof window !== 'undefined' ? window.localStorage?.getItem(CLAVE_EXPIRA) : null;

    if (tokenViejo) {
      await guardar(CLAVE_TOKEN, tokenViejo);
      window.localStorage.removeItem(CLAVE_TOKEN);
    }
    if (refreshViejo) {
      await guardar(CLAVE_REFRESH, refreshViejo);
      window.localStorage.removeItem(CLAVE_REFRESH);
    }
    if (expiraViejo) {
      await guardar(CLAVE_EXPIRA, expiraViejo);
      window.localStorage.removeItem(CLAVE_EXPIRA);
    }
  } catch {
    // No interrumpir si el entorno bloquea acceso a storage
  }
}

export async function guardar_sesion(token, expira_en, refresh_token, refresh_expira_en) {
  token_en_memoria = token;
  await guardar(CLAVE_TOKEN, token);
  await guardar(CLAVE_EXPIRA, expira_en);
  await guardar(CLAVE_REFRESH, refresh_token);
  return refresh_expira_en;
}

export async function restaurar_sesion() {
  await migrar_claves_inseguras_si_existen();
  token_en_memoria = await leer(CLAVE_TOKEN);
  return token_en_memoria;
}

export async function borrar_sesion() {
  token_en_memoria = null;
  await SecureStorage.remove(CLAVE_TOKEN);
  await SecureStorage.remove(CLAVE_REFRESH);
  await SecureStorage.remove(CLAVE_EXPIRA);
}

export function obtener_token() {
  return token_en_memoria;
}

export function obtener_refresh_token() {
  return leer(CLAVE_REFRESH);
}

// Inyecta la cabecera Bearer solo si existe un token en memoria
export function cabecera_autorizacion() {
  return token_en_memoria ? { Authorization: `Bearer ${token_en_memoria}` } : {};
}

// Garantiza que el token esté en memoria antes de una petición autenticada
export async function asegurar_token() {
  if (!token_en_memoria) {
    await restaurar_sesion();
  }
  return token_en_memoria;
}

// Suscripción desacoplada para evitar dependencias circulares con los stores
export function al_expirar(callback) {
  al_expirar_sesion = callback;
}

export function notificar_sesion_expirada() {
  token_en_memoria = null;
  if (al_expirar_sesion) al_expirar_sesion();
}
