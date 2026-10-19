import { ref, computed } from 'vue';
import { camara_disponible, tomar_foto } from '@/services/camara_service';
import { vibrar_toque, vibrar_error } from '@/services/vibracion_service';

/**
 * Composable para captura fotográfica (cámara nativa, galería de fotos o archivo local).
 * Genera la botonera de ActionSheet y maneja la delegación de archivos.
 *
 * @param {Object} options
 * @param {Function} options.onFotoCapturada - Callback ejecutado con el File / Blob seleccionado
 * @param {Function} [options.onError] - Callback en caso de error o advertencia
 */
export function useFotoProducto(options = {}) {
  const { onFotoCapturada, onError } = options;
  const mostrarActionSheet = ref(false);
  const inputArchivoWeb = ref(null);

  const botones = computed(() => {
    const list = [];
    if (camara_disponible()) {
      list.push({
        text: '📷 Tomar Foto con Cámara',
        handler: () => capturar('camara')
      });
      list.push({
        text: '🖼 Elegir de Galería / Fotos',
        handler: () => capturar('galeria')
      });
    }
    list.push({
      text: '📁 Seleccionar Archivo (PC / Web)',
      handler: () => {
        if (inputArchivoWeb.value) {
          inputArchivoWeb.value.click();
        }
      }
    });
    list.push({
      text: 'Cancelar',
      role: 'cancel'
    });
    return list;
  });

  const abrirSelector = () => {
    mostrarActionSheet.value = true;
  };

  const capturar = async (origen) => {
    mostrarActionSheet.value = false;
    vibrar_toque();
    const res = await tomar_foto(origen, { prefijo: 'producto', width: 900, quality: 65 });
    if (res.ok && res.archivo) {
      if (onFotoCapturada) {
        await onFotoCapturada(res.archivo);
      }
    } else if (res.mensaje && !res.cancelado) {
      await vibrar_error();
      if (onError) {
        onError(res.mensaje);
      }
    }
  };

  const alSeleccionarArchivoWeb = async (event) => {
    const archivo = event?.target?.files?.[0];
    if (archivo && onFotoCapturada) {
      await onFotoCapturada(archivo);
    }
    if (event?.target) event.target.value = '';
  };

  return {
    mostrarActionSheet,
    inputArchivoWeb,
    botones,
    abrirSelector,
    capturar,
    alSeleccionarArchivoWeb
  };
}
