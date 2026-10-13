import { LocalNotifications } from '@capacitor/local-notifications';
import { PushNotifications } from '@capacitor/push-notifications';
import { Capacitor } from '@capacitor/core';
import { ajax_service } from './ajax_service';

export const notificaciones_service = {
  /**
   * Inicializa notificaciones locales y push de manera defensiva.
   * Si falta google-services.json en Android, no rompe la app (graceful fallback).
   */
  async inicializar() {
    try {
      if (Capacitor.isNativePlatform()) {
        // 1. Notificaciones Locales: solicitar permisos si hace falta
        const permLocal = await LocalNotifications.checkPermissions();
        if (permLocal.display !== 'granted') {
          await LocalNotifications.requestPermissions();
        }

        // 2. Push Notifications: defensivo contra falta de FCM / google-services.json
        try {
          const permPush = await PushNotifications.checkPermissions();
          if (permPush.receive !== 'granted') {
            await PushNotifications.requestPermissions();
          }
          await PushNotifications.register();
          
          PushNotifications.addListener('registration', (token) => {
            console.log('[Push] Token FCM registrado exitosamente:', token.value);
          });
          PushNotifications.addListener('registrationError', (err) => {
            console.warn('[Push] Error de registro (normal si no hay google-services.json):', err);
          });
        } catch (pushErr) {
          console.warn('[Push] Inicialización omitida (sin configuración FCM):', pushErr);
        }
      }
    } catch (err) {
      console.warn('[NotificacionesService] Error general al inicializar:', err);
    }
  },

  /**
   * Dispara una notificación local inmediata en el dispositivo
   */
  async disparar_notificacion_local(titulo, cuerpo, id = Math.floor(Math.random() * 100000)) {
    try {
      if (Capacitor.isNativePlatform()) {
        await LocalNotifications.schedule({
          notifications: [
            {
              title: titulo,
              body: cuerpo,
              id: id,
              schedule: { at: new Date(Date.now() + 500) }, // 500ms
              sound: undefined,
              actionTypeId: '',
              extra: null
            }
          ]
        });
      } else {
        // En entorno web / testing local
        console.log(`[LocalNotification Simulado] ${titulo}: ${cuerpo}`);
      }
    } catch (err) {
      console.warn('[NotificacionesService] Error al disparar notificación local:', err);
    }
  },

  /**
   * Consume la bandeja de notificaciones in-app desde la API
   */
  async obtener_bandeja(limite = 20) {
    return ajax_service.get(`api/notificaciones?limite=${limite}`);
  },

  /**
   * Marca una notificación como leída
   */
  async marcar_leida(id) {
    return ajax_service.put(`api/notificaciones/${id}/leer`);
  },

  /**
   * Marca todas las notificaciones como leídas
   */
  async marcar_todas_leidas() {
    return ajax_service.put('api/notificaciones/marcar-todas-leidas');
  }
};
