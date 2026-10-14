import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { Capacitor } from '@capacitor/core';

// Cámara y galería nativas con flujo de permisos con final abierto.
// Devuelve siempre { ok, archivo, mensaje, cancelado }

const origenes = {
  camara: CameraSource.Camera,
  galeria: CameraSource.Photos
};

const alias_permiso = {
  camara: 'camera',
  galeria: 'photos'
};

export function camara_disponible() {
  return Capacitor.isNativePlatform();
}

export async function tomar_foto(origen = 'camara', opciones = {}) {
  if (!camara_disponible()) {
    return {
      ok: false,
      archivo: null,
      mensaje: 'La cámara nativa solo funciona en el dispositivo móvil instalado.',
      cancelado: false
    };
  }

  const anchoDeseado = opciones.width || (opciones.prefijo === 'producto' ? 900 : 1200);
  const calidadDeseada = opciones.quality || (opciones.prefijo === 'producto' ? 65 : 70);
  const prefijoNombre = opciones.prefijo || 'foto';

  try {
    const permiso = await asegurar_permiso(origen);
    if (!permiso.ok) return permiso;

    const foto = await Camera.getPhoto({
      source: origenes[origen] || CameraSource.Camera,
      resultType: CameraResultType.Uri,
      quality: calidadDeseada,
      width: anchoDeseado,
      correctOrientation: true,
      allowEditing: false,
      saveToGallery: false
    });

    const archivo = await foto_a_archivo(foto, prefijoNombre);
    const archivoOptimizado = await comprimir_imagen(archivo, anchoDeseado, anchoDeseado, calidadDeseada / 100);
    return { ok: true, archivo: archivoOptimizado, mensaje: null, cancelado: false };
  } catch (error) {
    if (es_cancelacion(error)) {
      return { ok: false, archivo: null, mensaje: null, cancelado: true };
    }
    return {
      ok: false,
      archivo: null,
      mensaje: (error && error.message) || 'No se pudo capturar la imagen.',
      cancelado: false
    };
  }
}

async function asegurar_permiso(origen) {
  const alias = alias_permiso[origen] || 'camera';
  const estado = await Camera.checkPermissions();
  if (permiso_concedido(estado && estado[alias])) {
    return { ok: true, archivo: null, mensaje: null, cancelado: false };
  }

  const pedido = await Camera.requestPermissions({ permissions: [alias] });
  if (permiso_concedido(pedido && pedido[alias])) {
    return { ok: true, archivo: null, mensaje: null, cancelado: false };
  }

  return {
    ok: false,
    archivo: null,
    mensaje: origen === 'galeria'
      ? 'Permiso de fotos denegado. Podés habilitarlo en los ajustes del dispositivo.'
      : 'Permiso de cámara denegado. Podés habilitarlo en los ajustes del dispositivo.',
    cancelado: false
  };
}

function permiso_concedido(estado) {
  return estado === 'granted' || estado === 'limited';
}

async function foto_a_archivo(foto, prefijo = 'foto') {
  const ruta = (foto && foto.webPath) || (foto && foto.path);
  if (!ruta) throw new Error('La cámara no devolvió ninguna imagen.');
  const respuesta = await fetch(ruta);
  const blob = await respuesta.blob();
  const extension = foto.format || 'jpeg';
  return new File([blob], `${prefijo}_${Date.now()}.${extension}`, {
    type: blob.type || `image/${extension}`,
    lastModified: Date.now()
  });
}

function es_cancelacion(error) {
  const mensaje = String((error && error.message) || '');
  return /cancel/i.test(mensaje) || /no image (picked|selected)/i.test(mensaje);
}

/**
 * Comprime una imagen (File o Blob) usando Canvas si excede dimensiones o peso,
 * preservando legibilidad de comprobantes y reduciendo drásticamente el consumo de memoria y ancho de banda.
 */
export async function comprimir_imagen(archivo, maxAncho = 1200, maxAlto = 1200, calidad = 0.72) {
  if (!archivo || !(archivo instanceof Blob) || !archivo.type.startsWith('image/')) {
    return archivo;
  }

  // Si ya es un archivo muy liviano (< 250 KB), no hace falta recomprimir
  if (archivo.size && archivo.size < 250 * 1024) {
    return archivo;
  }

  return new Promise((resolve) => {
    const url = URL.createObjectURL(archivo);
    const img = new Image();

    img.onload = () => {
      URL.revokeObjectURL(url);
      let ancho = img.width;
      let alto = img.height;

      if (ancho > maxAncho || alto > maxAlto) {
        if (ancho > alto) {
          alto = Math.round((alto * maxAncho) / ancho);
          ancho = maxAncho;
        } else {
          ancho = Math.round((ancho * maxAlto) / alto);
          alto = maxAlto;
        }
      }

      const canvas = document.createElement('canvas');
      canvas.width = ancho;
      canvas.height = alto;
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        resolve(archivo);
        return;
      }

      ctx.drawImage(img, 0, 0, ancho, alto);

      const formato = archivo.type === 'image/png' ? 'image/png' : 'image/jpeg';
      canvas.toBlob(
        (blob) => {
          if (!blob || blob.size >= archivo.size) {
            // Si por alguna razón el blob resultante es más pesado, conservamos el original
            resolve(archivo);
            return;
          }
          const nombreArchivo = (archivo.name || `imagen_${Date.now()}`).replace(/\.[^/.]+$/, '') + (formato === 'image/jpeg' ? '.jpg' : '.png');
          const nuevoArchivo = new File([blob], nombreArchivo, {
            type: formato,
            lastModified: Date.now()
          });
          resolve(nuevoArchivo);
        },
        formato,
        calidad
      );
    };

    img.onerror = () => {
      URL.revokeObjectURL(url);
      resolve(archivo);
    };

    img.src = url;
  });
}

