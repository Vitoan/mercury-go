import { Geolocation } from '@capacitor/geolocation';
import { Capacitor } from '@capacitor/core';

export const geolocalizacion_service = {
  /**
   * Obtiene la posición GPS actual del dispositivo (latitud, longitud).
   * Maneja permisos y timeouts defensivamente.
   */
  async obtener_posicion_actual() {
    try {
      if (Capacitor.isNativePlatform()) {
        const permisos = await Geolocation.checkPermissions();
        if (permisos.location !== 'granted') {
          const solicitud = await Geolocation.requestPermissions();
          if (solicitud.location !== 'granted') {
            throw new Error('Permiso de geolocalización no concedido por el usuario.');
          }
        }
      }

      const position = await Geolocation.getCurrentPosition({
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 5000
      });

      return {
        latitud: position.coords.latitude,
        longitud: position.coords.longitude,
        precision: position.coords.accuracy
      };
    } catch (error) {
      console.warn('[GeolocalizacionService] Error al obtener GPS:', error);
      throw error;
    }
  },

  /**
   * Abre Google Maps externo con las coordenadas indicadas
   * Cumple con el requerimiento de la Semana 7 (abrir mapa externo).
   */
  abrir_en_google_maps(latitud, longitud, etiqueta = '') {
    if (!latitud || !longitud) return;

    // URL estándar compatible tanto en navegador como en Android (dispara Intent de Maps o navegador)
    const url = `https://www.google.com/maps/search/?api=1&query=${latitud},${longitud}`;
    window.open(url, '_system');
  }
};
