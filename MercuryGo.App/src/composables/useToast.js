import { ref } from 'vue';

/**
 * Composable reutilizable para mostrar feedback con ion-toast en cualquier vista.
 * Encapsula las 3 refs (visible, mensaje, color) y expone helpers tipados por semántica.
 *
 * Uso en el template:
 *   <ion-toast :is-open="toast.visible.value" :message="toast.mensaje.value"
 *     :color="toast.color.value" duration="3000" @didDismiss="toast.ocultar" />
 *
 * Uso en el script:
 *   const toast = useToast();
 *   toast.mostrarExito('¡Guardado!');
 */
export function useToast() {
  const visible = ref(false);
  const mensaje = ref('');
  const color = ref('success');

  function mostrar(msg, colorToast = 'success') {
    mensaje.value = msg;
    color.value = colorToast;
    visible.value = true;
  }

  function mostrarExito(msg) {
    mostrar(msg, 'success');
  }

  function mostrarError(msg) {
    mostrar(msg, 'danger');
  }

  function mostrarInfo(msg) {
    mostrar(msg, 'primary');
  }

  function mostrarAdvertencia(msg) {
    mostrar(msg, 'warning');
  }

  function ocultar() {
    visible.value = false;
  }

  return {
    visible,
    mensaje,
    color,
    mostrar,
    mostrarExito,
    mostrarError,
    mostrarInfo,
    mostrarAdvertencia,
    ocultar
  };
}
