import { LocalNotifications } from '@capacitor/local-notifications';
import { PushNotifications } from '@capacitor/push-notifications';
import { Capacitor } from '@capacitor/core';
import { ajax_service } from './ajax_service';

let inicializado = false;

export const notificaciones_service = {
  /**
   * Inicializa notificaciones locales y push siguiendo la convención de la cátedra (Semana 7).
   * Si falta google-services.json en Android o VITE_PUSH_NOTIFICATIONS_ENABLED es 'false',
   * se omite el registro Push para no provocar cierre inesperado de la app.
   */
  async inicializar() {
    if (inicializado) return;
    inicializado = true;

    await this.preparar_locales();
    await this.preparar_push();
  },

  async preparar_locales() {
    try {
      if (!Capacitor.isPluginAvailable('LocalNotifications')) return;
      const permisos = await LocalNotifications.checkPermissions();
      if (permisos.display !== 'granted') {
        await LocalNotifications.requestPermissions();
      }
    } catch (err) {
      console.warn('[Notificaciones] Error al preparar locales:', err);
    }
  },

  async preparar_push() {
    // Si está deshabilitado por variable de entorno o no estamos en plataforma nativa, saltear
    if (import.meta.env.VITE_PUSH_NOTIFICATIONS_ENABLED === 'false') {
      console.info('[Push] Deshabilitado por configuración (VITE_PUSH_NOTIFICATIONS_ENABLED=false).');
      return;
    }

    if (!Capacitor.isNativePlatform() || !Capacitor.isPluginAvailable('PushNotifications')) {
      return;
    }

    try {
      const permisos = await PushNotifications.checkPermissions();
      if (permisos.receive !== 'granted') {
        const solicitados = await PushNotifications.requestPermissions();
        if (solicitados.receive !== 'granted') {
          console.warn('[Push] Permiso denegado por el usuario.');
          return;
        }
      }

      await PushNotifications.register();

      PushNotifications.addListener('registration', (token) => {
        console.info('[Push] Token registrado exitosamente:', token.value);
      });

      PushNotifications.addListener('registrationError', (error) => {
        console.warn('[Push] Error al registrar push:', error);
      });
    } catch (error) {
      console.warn('[Push] Push no inicializado. Revisá Firebase/google-services.json.', error);
    }
  },

  /**
   * Dispara una notificación local en el dispositivo.
   * Utiliza el cálculo de ID seguro (int32) de la cátedra para Android.
   */
  async disparar_notificacion_local(titulo, cuerpo) {
    if (!Capacitor.isPluginAvailable('LocalNotifications')) {
      console.log(`[LocalNotification Simulado] ${titulo}: ${cuerpo}`);
      return { ok: false, mensaje: null };
    }

    try {
      const permisos = await LocalNotifications.checkPermissions();
      if (permisos.display !== 'granted') {
        const solicitados = await LocalNotifications.requestPermissions();
        if (solicitados.display !== 'granted') {
          return { ok: false, mensaje: 'Sin permiso de notificaciones.' };
        }
      }

      await LocalNotifications.schedule({
        notifications: [
          {
            id: Date.now() % 2147483647,
            title: titulo,
            body: cuerpo,
            schedule: { at: new Date(Date.now() + 300) },
            sound: undefined,
            extra: null
          }
        ]
      });

      return { ok: true, mensaje: null };
    } catch (err) {
      console.warn('[Notificaciones] Error al programar local:', err);
      return { ok: false, mensaje: err.message };
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
