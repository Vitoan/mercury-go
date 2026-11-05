import { LocalNotifications } from '@capacitor/local-notifications';
import { PushNotifications } from '@capacitor/push-notifications';
import { Capacitor } from '@capacitor/core';
import { ajax_service } from './ajax_service';
import router from '@/router';

let inicializado = false;
let token_push = null;

// Canal Android de alta importancia (Semana 9):
// Permite que las notificaciones push y locales aparezcan como banners flotantes (heads-up)
// al instante, incluso con la pantalla apagada.
const CANAL_AVISOS = {
  id: 'avisos',
  name: 'Avisos y Pedidos MercuryGO',
  description: 'Cambios de estado de pedidos, asignaciones de chofer y avisos logísticos.',
  importance: 5,
  visibility: 1,
  vibration: true
};

export const notificaciones_service = {
  /**
   * Inicializa notificaciones locales y push nativas (Semana 9).
   */
  async inicializar() {
    if (inicializado) return;
    inicializado = true;

    await this.preparar_locales();
    await this.preparar_push();
  },

  obtener_token_push() {
    return token_push;
  },

  /**
   * Registra el token FCM del dispositivo en el backend (/api/dispositivos).
   * Se invoca al recibir el token de Firebase o al iniciar/restaurar sesión.
   */
  async registrar_dispositivo_en_api(token = null) {
    const t = token || token_push;
    if (!t) return;

    // Solo se registra si hay token de autenticación JWT vigente en el cliente
    const authToken = localStorage.getItem('token');
    if (!authToken) return;

    try {
      await ajax_service.put('/api/dispositivos', { token: t });
      console.info('[Push] Dispositivo registrado exitosamente en la API.');
    } catch (err) {
      console.warn('[Push] Error al registrar dispositivo en la API:', err);
    }
  },

  async preparar_locales() {
    try {
      if (!Capacitor.isPluginAvailable('LocalNotifications')) return;

      if (Capacitor.isNativePlatform()) {
        await LocalNotifications.createChannel(CANAL_AVISOS);
      }

      // Listener para navegación al tocar notificación local replicada
      LocalNotifications.addListener('localNotificationActionPerformed', (accion) => {
        const datos = accion.notification?.extra || {};
        if (datos.pedido_id) {
          router.push({ path: '/app/pedidos', query: { pedido_id: datos.pedido_id } });
        }
      });

      const permisos = await LocalNotifications.checkPermissions();
      if (permisos.display !== 'granted') {
        await LocalNotifications.requestPermissions();
      }
    } catch (err) {
      console.warn('[Notificaciones] Error al preparar locales:', err);
    }
  },

  async preparar_push() {
    if (import.meta.env.VITE_PUSH_NOTIFICATIONS_ENABLED === 'false') {
      console.info('[Push] Deshabilitado por configuración (VITE_PUSH_NOTIFICATIONS_ENABLED=false).');
      return;
    }

    if (!Capacitor.isNativePlatform() || !Capacitor.isPluginAvailable('PushNotifications')) {
      return;
    }

    try {
      // 1. Escuchar registro de token FCM
      PushNotifications.addListener('registration', async (token) => {
        token_push = token.value;
        console.info('[Push] Token FCM obtenido:', token_push);
        await this.registrar_dispositivo_en_api(token_push);
      });

      PushNotifications.addListener('registrationError', (error) => {
        console.warn('[Push] Error en registro FCM:', error);
      });

      // 2. Con la app abierta en primer plano (foreground), Android no muestra el banner:
      // Se repite como notificación local en el canal de alta importancia con los mismos datos.
      PushNotifications.addListener('pushNotificationReceived', async (push) => {
        console.info('[Push] Recibida en primer plano:', push);
        await this.disparar_notificacion_local(
          push.title || 'MercuryGO',
          push.body || '',
          push.data || {}
        );
      });

      // 3. Deep Linking: al presionar la notificación push, navegar al pedido correspondiente
      PushNotifications.addListener('pushNotificationActionPerformed', (accion) => {
        const datos = accion.notification?.data || {};
        console.info('[Push] Notificación tocada:', datos);
        if (datos.pedido_id) {
          router.push({ path: '/app/pedidos', query: { pedido_id: datos.pedido_id } });
        }
      });

      // 4. Solicitar permisos y registrar en FCM
      const permisos = await PushNotifications.checkPermissions();
      if (permisos.receive !== 'granted') {
        const solicitados = await PushNotifications.requestPermissions();
        if (solicitados.receive !== 'granted') {
          console.warn('[Push] Permiso denegado por el usuario.');
          return;
        }
      }

      await PushNotifications.register();
    } catch (error) {
      console.warn('[Push] Push no inicializado. Revisá Firebase/google-services.json.', error);
    }
  },

  /**
   * Dispara una notificación local en el canal de alta prioridad con ID int32 seguro.
   */
  async disparar_notificacion_local(titulo, cuerpo, datos = {}) {
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
            channelId: CANAL_AVISOS.id,
            extra: datos
          }
        ]
      });

      return { ok: true, mensaje: null };
    } catch (err) {
      console.warn('[Notificaciones] Error al programar local:', err);
      return { ok: false, mensaje: err.message };
    }
  },

  async obtener_bandeja(limite = 20) {
    return ajax_service.get(`api/notificaciones?limite=${limite}`);
  },

  async marcar_leida(id) {
    return ajax_service.put(`api/notificaciones/${id}/leer`);
  },

  async marcar_todas_leidas() {
    return ajax_service.put('api/notificaciones/marcar-todas-leidas');
  }
};
