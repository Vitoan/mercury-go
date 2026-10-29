// Clave única por operación de escritura. El servidor la usa para no repetir
// efectos si el cliente reintenta (doble tap, reconexión, reintento manual).
export function nueva_clave_idempotencia() {
  try {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }
  } catch {
    // Webviews sin crypto.randomUUID
  }
  return `op-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}
