import { BarcodeFormat, BarcodeScanner } from '@capacitor-mlkit/barcode-scanning';
import { Capacitor } from '@capacitor/core';

export function escaner_disponible() {
  return Capacitor.isNativePlatform();
}

export async function escanear_qr() {
  try {
    const soporte = await BarcodeScanner.isSupported();
    if (!soporte.supported) {
      return {
        ok: false,
        contenido: null,
        mensaje: 'Este dispositivo no cuenta con soporte para escanear códigos.'
      };
    }

    const modulo = await BarcodeScanner.isGoogleBarcodeScannerModuleAvailable();
    if (!modulo.available) {
      await BarcodeScanner.installGoogleBarcodeScannerModule();
      return {
        ok: false,
        contenido: null,
        mensaje: 'Descargando el módulo de escaneo de Google. Por favor, reintentá en unos instantes.'
      };
    }

    const resultado = await BarcodeScanner.scan({ formats: [BarcodeFormat.QrCode] });
    const contenido = (resultado?.barcodes?.[0]?.rawValue) || '';
    if (!contenido) {
      return {
        ok: false,
        contenido: null,
        mensaje: 'No se detectó ningún código QR.'
      };
    }

    return { ok: true, contenido, mensaje: null };
  } catch (error) {
    if (/cancel/i.test(String(error?.message || ''))) {
      return { ok: false, contenido: null, mensaje: null };
    }
    return {
      ok: false,
      contenido: null,
      mensaje: error?.message || 'No se pudo abrir el lector de códigos.'
    };
  }
}
