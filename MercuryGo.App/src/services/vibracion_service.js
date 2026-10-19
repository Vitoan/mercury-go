import { Haptics, ImpactStyle, NotificationType } from '@capacitor/haptics';

const CLAVE_VIBRACION = 'mercurygo_vibracion';

export function es_vibracion_activa() {
  const guardado = localStorage.getItem(CLAVE_VIBRACION);
  // Por defecto activa (true)
  return guardado !== null ? guardado === 'si' : true;
}

export function alternar_vibracion() {
  const activa = !es_vibracion_activa();
  localStorage.setItem(CLAVE_VIBRACION, activa ? 'si' : 'no');
  if (activa) {
    vibrar_toque();
  }
  return activa;
}

// Toque corto: respuesta táctil a una acción del usuario
export async function vibrar_toque() {
  if (!es_vibracion_activa()) return;
  try {
    await Haptics.impact({ style: ImpactStyle.Light });
  } catch {
    // Silencio si corre en navegador web sin motor háptico
  }
}

// Patrón de confirmación / éxito
export async function vibrar_exito() {
  if (!es_vibracion_activa()) return;
  try {
    await Haptics.notification({ type: NotificationType.Success });
  } catch {
    // Silencio en web
  }
}

// Patrón de advertencia
export async function vibrar_advertencia() {
  if (!es_vibracion_activa()) return;
  try {
    await Haptics.notification({ type: NotificationType.Warning });
  } catch {
    // Silencio en web
  }
}

// Patrón de error
export async function vibrar_error() {
  if (!es_vibracion_activa()) return;
  try {
    await Haptics.notification({ type: NotificationType.Error });
  } catch {
    // Silencio en web
  }
}
