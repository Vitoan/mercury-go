import { Capacitor } from '@capacitor/core';
import { Directory, Filesystem } from '@capacitor/filesystem';
import { Share } from '@capacitor/share';

// Compartir un binario (comprobante PDF o remito).
// En Android nativo se escribe en Filesystem (Directory.Cache) y se abre Share.share().
// En navegador web se utiliza la API navigator.share o descarga directa automática.

export async function compartir_archivo({ nombre_archivo, blob, titulo, texto }) {
  if (!blob) return { ok: false, mensaje: 'No hay archivo para compartir.' };

  try {
    if (!Capacitor.isNativePlatform()) {
      return await compartir_archivo_web({ nombre_archivo, blob, titulo, texto });
    }

    const base64 = await blob_a_base64(blob);
    const escrito = await Filesystem.writeFile({
      path: nombre_archivo,
      data: base64,
      directory: Directory.Cache
    });

    await Share.share({
      title: titulo,
      text: texto,
      files: [escrito.uri],
      dialogTitle: titulo || 'Compartir documento'
    });

    return { ok: true, mensaje: null };
  } catch (error) {
    if (/cancel/i.test(String(error?.message || ''))) return { ok: true, mensaje: null };
    return { ok: false, mensaje: error?.message || 'No se pudo compartir el archivo.' };
  }
}

async function compartir_archivo_web({ nombre_archivo, blob, titulo, texto }) {
  const archivo = new File([blob], nombre_archivo, { type: blob.type || 'application/pdf' });
  if (navigator.canShare && navigator.canShare({ files: [archivo] })) {
    try {
      await navigator.share({ title: titulo, text: texto, files: [archivo] });
      return { ok: true, mensaje: null };
    } catch (err) {
      if (/cancel/i.test(String(err?.message || ''))) return { ok: true, mensaje: null };
    }
  }
  descargar_en_navegador(nombre_archivo, blob);
  return { ok: true, mensaje: 'Se descargó el archivo en tu navegador.' };
}

function descargar_en_navegador(nombre_archivo, blob) {
  const url = window.URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = nombre_archivo;
  document.body.appendChild(enlace);
  enlace.click();
  document.body.removeChild(enlace);
  window.URL.revokeObjectURL(url);
}

function blob_a_base64(blob) {
  return new Promise((resolve, reject) => {
    const lector = new FileReader();
    lector.onerror = () => reject(new Error('No se pudo leer el archivo binario.'));
    lector.onload = () => {
      const res = String(lector.result || '');
      resolve(res.slice(res.indexOf(',') + 1));
    };
    lector.readAsDataURL(blob);
  });
}
