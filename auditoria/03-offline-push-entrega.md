# Auditoría Técnica: Offline, Push Notifications y Entrega — MercuryGO

**Fecha:** 2026-11-10  
**Commit HEAD:** `2960114417a912d936580e2b020816d946d1cd42`  
**Dispositivo Físico Conectado:** `R5CT509R1TA` (Samsung Android físico vía ADB)  
**Reglas aplicadas:** `/auditoria/00-reglas.md` (Solo lectura de código, verificación línea por línea contra código real, estado y visión en el teléfono).

---

## PARTE A: Mecánica Offline y Notificaciones Push

### 1. Resolución de Conflictos al Sincronizar (Gana el Chofer / Decisiones 10, 16 y 17)

En `MercuryGo.Api/Services/ConflictoOfflineService.cs`, la arquitectura implementa una política determinística ante colisiones entre el trabajo físico fuera de línea y mutaciones remotas:

#### A. Entrega sobre Pedido Cancelado (Decisión 10)
* **Archivo:línea:** `MercuryGo.Api/Services/ConflictoOfflineService.cs:18-33` (invocado desde `DespachoController.cs:605` y `PedidosController.cs:417`)
* **Código Real:**
```csharp
public async Task<int> AvisarEntregaSobreCanceladoAsync(
    Pedido pedido,
    long usuarioId,
    EstadoPedido estadoAnterior,
    CancellationToken ct = default)
{
    if (estadoAnterior != EstadoPedido.Cancelado || pedido.Estado != EstadoPedido.Entregado)
        return 0;

    var quien = await NombreAsync(usuarioId, ct);
    return await NotificarAsync(
        pedido,
        $"⚠️ Entrega offline sobre pedido cancelado ({pedido.Numero})",
        $"La orden {pedido.Numero} ya estaba cancelada, pero {quien} registró la entrega sin conexión. Se respetó la acción del chofer.",
        ct);
}
```
* **Comportamiento:** Si un pedido fue cancelado en depósito mientras el chofer estaba sin señal, al reconectar y enviar `ENTREGAR`, la transición a `Entregado` **prevalece** (gana la realidad física del reparto) y el servicio genera una notificación de advertencia de alta prioridad al Administrador indicando quién lo entregó.

#### B. Chofer Reasignado durante el Reparto Offline (Decisión 17)
* **Archivo:línea:** `MercuryGo.Api/Services/ConflictoOfflineService.cs:37-56`
* **Código Real:**
```csharp
public async Task<int> AvisarChoferReasignadoAsync(
    Pedido pedido,
    long usuarioId,
    bool esChofer,
    EstadoPedido estadoNuevo,
    CancellationToken ct = default)
{
    var esCierre = estadoNuevo is EstadoPedido.Entregado or EstadoPedido.NoEntregado;

    if (!esChofer || !esCierre || !pedido.ChoferId.HasValue || pedido.ChoferId.Value == usuarioId)
        return 0;

    var ejecutor = await NombreAsync(usuarioId, ct);
    var asignado = await NombreAsync(pedido.ChoferId.Value, ct);
    return await NotificarAsync(
        pedido,
        $"🔀 Entrega offline de un pedido reasignado ({pedido.Numero})",
        $"La orden {pedido.Numero} estaba asignada a {asignado}, pero {ejecutor} la entregó sin conexión. Se respetó la acción del chofer.",
        ct);
}
```
* **Comportamiento:** Si la orden fue reasignada a otro chofer en la base mientras el primero estaba sin cobertura, la entrega del chofer ejecutor es aceptada y se emite un aviso al Administrador alertando sobre la reasignación en conflicto.

#### C. Picking sobre Pedido Cancelado (Decisión 16)
* **Archivo:línea:** `MercuryGo.Api/Services/ConflictoOfflineService.cs:60-80`
* **Código Real:**
```csharp
public async Task<int> AvisarPickingSobreCanceladoAsync(
    Pedido pedido,
    long usuarioId,
    bool conFaltantes,
    bool esOffline,
    CancellationToken ct = default)
{
    if (!esOffline || pedido.Estado != EstadoPedido.Cancelado)
        return 0;

    var quien = await NombreAsync(usuarioId, ct);
    var detalle = conFaltantes ? " El picking quedó registrado con faltantes." : string.Empty;

    return await NotificarAsync(
        pedido,
        $"⚠️ Picking offline sobre pedido cancelado ({pedido.Numero})",
        $"La orden {pedido.Numero} ya estaba cancelada, pero {quien} preparó la mercadería sin conexión.{detalle}",
        ct);
}
```
* **Comportamiento:** El trabajo físico del operario de depósito queda registrado en el historial contable aunque la orden esté cancelada, alertando a supervisión.

---

### 2. Manejo de Errores en la Cola de Sincronización (HTTP 401 y 5xx)

* **Archivo:línea:** `MercuryGo.App/src/stores/offline_store.js:220-245`
* **Código Real:**
```javascript
try {
  await this.ejecutar_despacho_api(accion);
  accion.estado = 'enviada';
  accion.error = null;
  exitoAlMenosUno = true;
} catch (err) {
  const clasificacion = clasificar_error(err);
  accion.codigo = err?.codigo || null;
  accion.error = err?.mensaje || err?.message || 'Rechazado por servidor.';

  if (err?.estado_http === 401) {
    // ACT-03 / DEF-31: Sesión expirada o token no renovado.
    // Pausar sincronización sin descartar ni marcar como conflicto irrecuperable.
    accion.estado = 'pendiente';
    this.guardar_cola([...this.cola]);
    break;
  }

  // No continuar con las siguientes acciones de ESTE pedido (FIFO).
  accion.estado = clasificacion;
  if (clasificacion === 'conflicto') {
    conflictoOcurrido = true;
    this.bloquear_posteriores(pId, accion);
  }
  break;
}
```
* **Comportamiento ante HTTP 401 (Sesión Expirada):**
  * La sincronización **no borra la cola ni marca los ítems como conflicto**.
  * Deja el elemento en estado `pendiente`, detiene el bucle y conserva la cola íntegra en el dispositivo para que cuando el usuario renueve la sesión o reingrese credenciales se reanude el envío sin pérdida de datos.
* **Comportamiento ante HTTP 5xx o Pérdida de Red:**
  * El error se clasifica como `'error'` transitorio. El elemento permanece en la cola como `pendiente` o `error` y se reintenta automáticamente al dispararse el evento `networkStatusChange` de Capacitor Network.

---

### 3. Deep Linking y Notificaciones Push (App Cerrada y Token Rotado)

* **Deep Linking con la App Cerrada / Segundo Plano:**
  * `MercuryGo.App/src/services/notificaciones_service.js:118-124`:
  ```javascript
  PushNotifications.addListener('pushNotificationActionPerformed', (accion) => {
    const datos = accion.notification?.data || {};
    console.info('[Push] Notificación tocada:', datos);
    if (datos.pedido_id) {
      router.push({ path: '/app/pedidos', query: { pedido_id: datos.pedido_id } });
    }
  });
  ```
  * En Android nativo, al pulsar una notificación push con la app cerrada, el Intent de lanzamiento pasa los datos a `MainActivity` y el listener `pushNotificationActionPerformed` navega a `/app/pedidos?pedido_id={id}`.
* **Token FCM Rotado o Inválido:**
  * Si Firebase rota el token FCM en el dispositivo, se dispara el listener `registration` (`notificaciones_service.js:96-100`):
  ```javascript
  PushNotifications.addListener('registration', async (token) => {
    token_push = token.value;
    console.info('[Push] Token FCM obtenido:', token_push);
    await this.registrar_dispositivo_en_api(token_push);
  });
  ```
  * Invoca `PUT /api/dispositivos` actualizando el token en la base de datos para ese `UsuarioId`.
  * En el Backend, si Firebase responde `Unregistered` o `InvalidArgument` al despachar una push, `EnvioPush.cs` captura la excepción sin interrumpir la transacción del pedido y purga el token obsoleto de la base de datos.

---

## PARTE B: Estado de Entrega, Configuración y Verificación Nativa

### 1. Detección de Direcciones Locales y Esquemas HTTP
* **Búsqueda realizada:** `grep -rn "localhost|10.0.2.2|192.168|http://" MercuryGo.App/src`
* **Resultado:**
  * No hay URLs duras a `localhost`, `10.0.2.2` ni `192.168.*` en el código de producción.
  * La única coincidencia de `http://` es un validador sintáctico en `MercuryGo.App/src/utils/imagenes.js:9` (`url.startsWith('http://') || url.startsWith('https://')`).
  * En `MercuryGo.App/src/config/debug.js:3`, `api_url_remota` está configurada estrictamente como:
  ```javascript
  const api_url_remota = 'https://mercurygo.runasp.net';
  ```
  * En `MercuryGo.App/.env.release` y `.env.production`:
  ```ini
  VITE_API_URL=https://mercurygo.runasp.net
  VITE_PUSH_NOTIFICATIONS_ENABLED=true
  ```

---

### 2. Configuración Gradle y Firma del APK
* **Archivo:** `MercuryGo.App/android/app/build.gradle`
* **Configuración de Firma (Signing Config):**
```gradle
signingConfigs {
    release {
        if (keystorePropertiesFile.exists()) {
            storeFile rootProject.file(keystoreProperties['storeFile'])
            storePassword keystoreProperties['storePassword']
            keyAlias keystoreProperties['keyAlias']
            keyPassword keystoreProperties['keyPassword']
        }
    }
}
buildTypes {
    release {
        if (keystorePropertiesFile.exists()) {
            signingConfig signingConfigs.release
        }
        minifyEnabled false
        proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-rules.pro'
    }
}
```
* **Verificación de Firma:** `keystore.properties` existe en el directorio de build y el APK `app-release.apk` fue verificado con `apksigner`:
  * **Esquema de firma v2:** `true`.
  * **Nombre de versión:** `1.0` (`versionCode = 1`).
  * **Identificador:** `io.ionic.starter` (coincidente con `google-services.json`).

---

### 3. Permisos en AndroidManifest.xml
* **Archivo:** `MercuryGo.App/android/app/src/main/AndroidManifest.xml:38-43`
* **Permisos declarados:**
```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
```
* Cubren de forma exacta los requerimientos de cámara (M06), geolocalización GPS (M07) y conectividad de red (M08/M09).

---

### 4. Verificación de API en Nube: Health Check y HTTPS
* **Endpoint:** `https://mercurygo.runasp.net/health`
* **Comprobación en vivo:**
```http
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
Server: Microsoft-IIS/10.0
X-Powered-By: ASP.NET

{"status":"ok","utc":"2026-11-11T00:28:02.1956574+00:00"}
```
* Responde `200 OK` sobre HTTPS con certificado TLS válido y activo.

---

### 5. Verificación en Dispositivo Físico Conectado (`R5CT509R1TA`)
* **Dispositivo detectado:** Samsung Android (`R5CT509R1TA` conectado vía ADB).
* **Inspección de paquete instalado:**
  * **Comando:** `adb -s R5CT509R1TA shell dumpsys package io.ionic.starter`
  * **Resultado:**
    * Paquete: `package:io.ionic.starter` **INSTALADO**.
    * `versionCode=1`, `versionName=1.0`.
    * Firma instalada: `signatures=PackageSignatures{c0e7bfb version:2, signatures:[caa183cf]}`.
    * Estado: **VERIFICADO EN HARDWARE REAL**.

---

## Hallazgos de Esta Auditoría

* **Total de Hallazgos:**
  * **CRÍTICO:** 0
  * **ALTO:** 0
  * **MEDIO:** 0
  * **BAJO:** 0
* **Conclusión:** La mecánica de sincronización offline, el ciclo de vida de notificaciones push y los requisitos de empaquetado/firma release cumplen con todos los criterios evaluados.
