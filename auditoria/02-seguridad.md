# Auditoría de Seguridad con Ojo de Atacante — MercuryGO

**Fecha:** 2026-11-10  
**Commit HEAD:** `2960114417a912d936580e2b020816d946d1cd42`  
**Reglas aplicadas:** `/auditoria/00-reglas.md` (Solo lectura de código, verificación línea por línea contra código real, estado y visión en el teléfono).

---

## 1. Matriz de Autorización de Endpoints (Controladores API)

Tabla completa de los endpoints en `MercuryGo.Api/Controllers` con sus roles configurados y evaluación de permisos:

| Controlador | Método HTTP | Ruta | Roles Permitidos / Atributo | Evaluación de Seguridad |
| :--- | :---: | :--- | :--- | :--- |
| `CategoriasController` | `GET` | `api/categorias` | `[AllowAnonymous]` | **OK** (Catálogo público de categorías) |
| `CategoriasController` | `POST` | `api/categorias` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `CategoriasController` | `PUT` | `api/categorias/{id}` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `CategoriasController` | `DELETE` | `api/categorias/{id}` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `ClientesController` | `GET` | `api/clientes` | `ADMIN,OPERARIO` | **OK** (Paginado de clientes para gestión) |
| `ClientesController` | `GET` | `api/clientes/{id}` | Clase: `ADMIN,OPERARIO,CLIENTE` | **OK** (Valida en código: `CLIENTE` solo consulta si `id == User.ClienteId()`) |
| `ClientesController` | `POST` | `api/clientes` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `ClientesController` | `PUT` | `api/clientes/{id}` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `ClientesController` | `DELETE` | `api/clientes/{id}` | `[Authorize(Roles = "ADMIN")]` | **OK** |
| `CuentasCorrientesController` | `POST` | `api/pedidos/{id}/cobro-chofer` | `ADMIN,CHOFER` | **OK** (Valida `pedido.ChoferId == User.UsuarioId()`) |
| `CuentasCorrientesController` | `GET` | `api/pedidos/cuenta-corriente` | `ADMIN,CLIENTE` | **OK** (Cliente solo ve su saldo comercial) |
| `CuentasCorrientesController` | `GET` | `api/pedidos/cuentas-corrientes-clientes` | `ADMIN` | **OK** (Auditoría consolidada comercial) |
| `CuentasCorrientesController` | `PUT` | `api/pedidos/{id}/aprobar-comprobante` | `ADMIN` | **OK** |
| `CuentasCorrientesController` | `PUT` | `api/pedidos/{id}/rechazar-comprobante` | `ADMIN` | **OK** |
| `CuentasCorrientesController` | `POST` | `api/pedidos/cuenta-corriente/pagos` | `ADMIN,CLIENTE` | **OK** |
| `CuentasCorrientesController` | `GET` | `api/pedidos/cuenta-corriente/pagos` | `ADMIN,CLIENTE` | **OK** (Cliente filtra estrictamente por su `cliente.Id`) |
| `CuentasCorrientesController` | `PUT` | `api/pedidos/cuenta-corriente/pagos/{id}/aprobar` | `ADMIN` | **OK** |
| `CuentasCorrientesController` | `PUT` | `api/pedidos/cuenta-corriente/pagos/{id}/rechazar` | `ADMIN` | **OK** |
| `CuentasCorrientesController` | `GET` | `api/pedidos/{id}/comprobante-pago/archivo` | Clase: `ADMIN,OPERARIO,CHOFER,CLIENTE` | **OK** (Filtrado por `PuedeAccederAlPedidoAsync` y sandbox de uploads) |
| `CuentasCorrientesController` | `GET` | `api/pedidos/cuenta-corriente/pagos/{id}/archivo` | `ADMIN,CLIENTE` | **OK** (Cliente solo descarga si `pago.ClienteId == cliente.Id`) |
| `CuentasCorrientesController` | `POST` | `api/pedidos/{id}/comprobante-pago` | Clase: `ADMIN,OPERARIO,CHOFER,CLIENTE` | **OK** (Validado por `PuedeAccederAlPedidoAsync`) |
| `DespachoController` | `GET` | `api/pedidos/despacho/hoja-ruta` | `ADMIN,CHOFER` | **OK** (Chofer filtra estrictamente por `ChoferId == User.UsuarioId()`) |
| `DespachoController` | `GET` | `api/pedidos/despacho/picking-pendientes` | `ADMIN,OPERARIO` | **OK** |
| `DespachoController` | `PUT` | `api/pedidos/{id}/picking` | `ADMIN,OPERARIO` | **OK** |
| `DespachoController` | `POST` | `api/pedidos/{id}/no-entregado` | `ADMIN,CHOFER` | **OK** (Valida `pedido.ChoferId == User.UsuarioId()`) |
| `DespachoController` | `POST` | `api/pedidos/{id}/reintentar-entrega` | `ADMIN` | **OK** |
| `DespachoController` | `POST` | `api/pedidos/{id}/entrega-parcial` | `ADMIN,CHOFER` | **OK** (Valida `pedido.ChoferId == User.UsuarioId()`) |
| `DispositivosController` | `PUT` | `api/dispositivos` | `[Authorize]` | **OK** (Registra token FCM asociado al `User.UsuarioId()`) |
| `DispositivosController` | `DELETE` | `api/dispositivos` | `[Authorize]` | **OK** (Desregistra token FCM del usuario conectado) |
| `NotificacionesController` | `GET` | `api/notificaciones` | `[Authorize]` | **OK** (Filtra `UsuarioId == User.UsuarioId()`) |
| `NotificacionesController` | `PUT` | `api/notificaciones/{id}/leer` | `[Authorize]` | **OK** (Filtra `UsuarioId == User.UsuarioId()`) |
| `NotificacionesController` | `PUT` | `api/notificaciones/marcar-todas-leidas` | `[Authorize]` | **OK** (Filtra `UsuarioId == User.UsuarioId()`) |
| `PedidosController` | `GET` | `api/pedidos` | `ADMIN,OPERARIO,CHOFER,CLIENTE` | **OK** (Segmentado por rol: Cliente ve sus pedidos, Chofer su ruta) |
| `PedidosController` | `POST` | `api/pedidos` | `ADMIN,OPERARIO,CLIENTE` | **OK** (Chofer excluido) |
| `PedidosController` | `PUT` | `api/pedidos/{id}/estado` | `ADMIN,OPERARIO,CHOFER,CLIENTE` | **OK** (Valida transiciones y propiedad por rol) |
| `PedidosController` | `PUT` | `api/pedidos/{id}` | `ADMIN` | **OK** (Edición de pedidos confirmados) |
| `PedidosController` | `PUT` | `api/pedidos/{id}/confirmar` | `ADMIN` | **OK** |
| `PedidosController` | `DELETE` | `api/pedidos/{id}` | `CLIENTE` | **OK** (Cancelación de órdenes no confirmadas del cliente propio) |
| `PedidosController` | `DELETE` | `api/pedidos/{id}/baja-admin` | `ADMIN` | **OK** |
| `ProductosController` | `GET` | `api/productos` | `[AllowAnonymous]` | **OK** |
| `ProductosController` | `GET` | `api/productos/{id}` | `[AllowAnonymous]` | **OK** |
| `ProductosController` | `POST/PUT/DELETE` | `api/productos` | `ADMIN` | **OK** |
| `SesionController` | `POST` | `api/sesion/login` | `[AllowAnonymous]` | **OK** (Protegido por `ILoginRateLimiter`) |
| `SesionController` | `POST` | `api/sesion/refresh` | `[AllowAnonymous]` | **OK** (Rotación estricta y hash SHA-256) |
| `SesionController` | `POST` | `api/sesion/logout` | `[AllowAnonymous]` | **OK** (Revoca token y limpia token FCM) |
| `SesionController` | `GET` | `api/sesion/yo` | `[Authorize]` | **OK** |
| `UsuariosController` | `GET/POST/PUT/DELETE`| `api/usuarios` | `ADMIN` | **OK** |

---

## 2. Aislamiento Horizontal y Multi-Tenant (IDOR)

### ¿Un CLIENTE puede ver o tocar pedidos de otro cliente?
* **No puede.** La consulta en `PedidosController.cs:42-48` aplica el filtro:
```csharp
if (User.EsCliente())
{
    var propio = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
    consulta = propio != null
        ? consulta.Where(p => p.ClienteId == propio.Id)
        : consulta.Where(p => false);
}
```
Y en mutaciones (`PedidosControllerBase.cs:43-47`):
```csharp
if (User.EsCliente())
{
    var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
    return cliente != null && pedido.ClienteId == cliente.Id;
}
```
El `cliente_id` se resuelve estrictamente desde el claim `cliente_id` del JWT o mediante la clave foránea `usuarios.cliente_id`.

### ¿Un CHOFER ve pedidos o rutas que no le tocan?
* **No ve rutas ajenas.** El filtro en `PedidosController.cs:57-67` y `DespachoController.cs:40-45` restringe la consulta:
```csharp
else if (User.IsInRole(RolCodigos.Chofer))
{
    var choferId = User.UsuarioId();
    consulta = consulta.Where(p => p.ChoferId == choferId);
```
Y en `PedidosControllerBase.cs:36-41`:
```csharp
if (User.IsInRole(RolCodigos.Chofer))
{
    var usuarioId = User.UsuarioId();
    return pedido.ChoferId.HasValue && pedido.ChoferId.Value == usuarioId;
}
```
Adicionalmente, en `DespachoController.cs` (`no-entregado`, `entrega-parcial`, `cobro-chofer`) se valida expresamente:
```csharp
if (User.IsInRole(RolCodigos.Chofer) && (!pedido.ChoferId.HasValue || pedido.ChoferId.Value != User.UsuarioId()))
    return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "chofer_no_asignado", mensaje = "No tenés asignada esta orden de carga." });
```

---

## 3. Asignación de Cliente y Fallbacks

* **Evaluación:** No existen clientes ni IDs fijos codificados por defecto ("hardcoded") en la API.
* **Resolución:**
  1. Primero se extrae el claim `cliente_id` emitido en el JWT (`TokenService.cs:38-41`).
  2. Si el claim no estuviera presente, `ResolverClienteDeUsuarioAsync` en `MercuryGoDbContext.cs:28-47` consulta `Usuarios.ClienteId` en BD por clave foránea. Si tampoco existe, recurre a coincidencia por `Clientes.Email == usuario.Email`.
  3. Si no hay coincidencia, retorna `null` y la API responde `HTTP 403 Forbidden` (`cliente_no_vinculado`), impidiendo cualquier operación de autoservicio a usuarios no vinculados.

---

## 4. Seguridad de JWT y Refresh Tokens

* **Expiración de Acceso:** 8 horas (`JwtOpciones.HorasVigencia = 8`).
* **Refresh Token:** Cadena criptográfica aleatoria de 48 bytes (`RandomNumberGenerator.GetBytes(48)`). En base de datos (`refresh_tokens`) **solo se almacena su hash SHA-256** (`TokenService.HashRefresh`).
* **Rotación en cada refresco:** En `SesionController.cs:98`, el token de refresco presentado se marca inmediatamente como revocado (`guardado.RevocadoEn = DateTimeOffset.UtcNow`) y se emite un par nuevo.
* **Revocación en Logout:** `SesionController.cs:124-154` revoca el refresh token en base de datos y borra el token push FCM registrado para ese dispositivo.
* **Invalidación Inmediata por Middleware:** `Program.cs:72-118` implementa `OnTokenValidated` en tiempo real; si el usuario es dado de baja en base de datos o su rol cambia, el token JWT vigente es rechazado inmediatamente sin esperar a que expire.

---

## 5. Almacenamiento Seguro de Credenciales en el Dispositivo

* **Búsqueda realizada:** `grep -rn "localStorage|sessionStorage" MercuryGo.App/src`
* **Resultado:**
  * Los tokens de acceso JWT y refresh tokens se gestionan **exclusivamente** mediante `@aparajita/capacitor-secure-storage` en `token_service.js:14-29` (utilizando Android Keystore / iOS Keychain).
  * `localStorage` se utiliza únicamente para configuraciones no sensibles de interfaz: tema visual (`mercurygo_tema`), estado de vibración (`mercurygo_vibracion`), clave de usuario previo (`mercurygo_ultimo_usuario_id`) y copias locales de lectura offline (`mercurygo_copia:*`).
  * `token_service.js:31-53` incluye una rutina de migración que purga de forma activa cualquier residuo de versiones previas que pudiera haber quedado en `localStorage`.

---

## 6. Idempotencia y Prevención de Duplicados

* **Implementación:** `IdempotenciaService.cs` registra cada operación procesada en la tabla `operaciones_procesadas` (`Id`, `Tipo`, `UsuarioId`, `PedidoId`, `ResultadoJson`, `ProcesadaEn`).
* **Verificación de Endpoints Críticos:**
  * `POST /api/pedidos` (Crear orden): Consulta `ObtenerOperacionAsync(idempotencyKey, "CREAR_PEDIDO")`; si existe, devuelve la respuesta previa sin duplicar inserciones ni descontar stock adicional.
  * `POST /api/pedidos/{id}/cobro-chofer` (Cobro en destino): Registra `COBRO_CHOFER`; reintentos no generan cobros duplicados en `cobros_pedido`.
  * `POST /api/pedidos/{id}/entrega-parcial`: Registra `ENTREGA_PARCIAL` con `RegistrarOperacionAsync`.
  * `POST /api/pedidos/{id}/reintentar-entrega`: Registra `REINTENTAR_ENTREGA`.
  * `POST /api/pedidos/{id}/no-entregado`: Registra `ENTREGA_FALLIDA`.
  * `PUT /api/pedidos/{id}/estado`: Registra `CAMBIAR_ESTADO`.

---

## 7. Cierre de Sesión (Logout) y Purga de Datos

En `sesion_store.js`:
* `salir()` ejecuta:
  1. `desregistrar_dispositivo_en_api()`: Da de baja el token FCM en la base de datos para evitar notificaciones cruzadas.
  2. `cerrar_sesion(refresh_token)`: Revoca el token en el backend.
  3. `limpiar()`:
     - Resetea `pedidos_store.resetear_store()`.
     - Ejecuta `carrito_store.vaciar_carrito()`.
     - Borra todas las copias en caché mediante `borrar_copias()`.
     - Borra la cola de acciones offline (`offlineStore.guardar_cola([])`).
     - Borra los tokens del almacenamiento seguro (`borrar_sesion()`).
* Adicionalmente, al iniciar sesión con una cuenta diferente (`sesion_store.js:84-88`), si el ID difiere de `mercurygo_ultimo_usuario_id`, se eliminan de forma preventiva todas las copias y colas locales para evitar fugas entre operadores del mismo dispositivo.

---

## 8. Revisión de Secretos en el Repositorio Git

* **Búsqueda ejecutada:** `git ls-files | grep -i -E "google-services|service-account|\.env|keystore|\.jks|appsettings"`
* **Archivos rastreados por Git:**
  * `MercuryGo.Api/appsettings.json` (Contiene cadenas de conexión locales de desarrollo; no contiene contraseñas de producción).
  * `MercuryGo.Api/appsettings.Development.json` (Configuración de entorno de desarrollo).
  * `MercuryGo.App/.env.development` (Variables de entorno para Vite en local).
  * `MercuryGo.App/.env.release` (Contiene URLs públicas y flags; no contiene tokens ni credenciales secretas).
  * `MercuryGo.App/android/keystore.properties.example` (Plantilla de ejemplo vacía).
* **Archivos sensibles EXCLUIDOS correctamente en `.gitignore`:**
  * `firebase-service-account.json` (Excluido).
  * `google-services.json` (Excluido).
  * `keystore.properties` (Excluido).
  * `*.jks` / `*.keystore` (Excluidos).
  * `appsettings.Production.json` (Excluido).
  * `.env.production` (Excluido).

---

## 9. Rate Limit, Carga de Archivos y CORS

* **Rate Limit de Autenticación:**
  * Implementado en `LoginRateLimiterService.cs` y aplicado en `SesionController.cs:47`.
  * Limita a un máximo de **5 intentos fallidos cada 15 minutos** por combinación de IP y correo electrónico. Superado el límite, responde `HTTP 429 Too Many Requests`.
* **Subida de Archivos:**
  * **Comprobantes Bancarios:** Restringido a extensiones `.pdf`, `.jpg`, `.jpeg`, `.png`, `.webp` y tamaño menor o igual a **5 MB** (`CuentasCorrientesController.cs:442-446, 668-672`).
  * **Protección contra Path Traversal (LFI):** La descarga de archivos en `CuentasCorrientesController.cs` valida que la ruta física resuelta esté estrictamente contenida dentro del directorio `wwwroot/uploads/comprobantes` mediante `StartsWith`, rechazando nombres con secuencias relativas `../`.
  * **Protección Estática:** `Program.cs:179-185` bloquea descargas anónimas directas sobre `/uploads/comprobantes` respondiendo `401 Unauthorized`.
* **CORS:**
  * Configurado en `Program.cs:142-161`.
  * En producción, restringe los orígenes a la lista declarada en `origenesProduccion` (dominios web autorizados y esquemas nativos `capacitor://localhost` / `ionic://localhost`).

---

## 10. Evaluación de Hipótesis de Riesgo

1. **Hipótesis: Autoservicio asignado a un cliente fijo.**
   * **DESCARTADA.** La identidad comercial se resuelve en tiempo de ejecución desde el JWT autenticado o la relación de base de datos; no existen clientes fijos.
2. **Hipótesis: Fuga de datos por valores por defecto.**
   * **DESCARTADA.** Si un usuario no posee un cliente asignado, la API deniega el acceso con `HTTP 403 Forbidden` (`cliente_no_vinculado`) en lugar de asumir un cliente predeterminado.
3. **Hipótesis: Logout que no vacía el carrito.**
   * **DESCARTADA.** `sesion_store.js:124` invoca directamente `useCarritoStore().vaciar_carrito()` durante la secuencia de limpieza atómica de sesión.
4. **Hipótesis: Cada chofer ve la ruta de todos.**
   * **DESCARTADA.** `PedidosController.cs:57-67` y `DespachoController.cs:40-45` imponen la cláusula `consulta = consulta.Where(p => p.ChoferId == choferId)` para el rol `CHOFER`.
5. **Hipótesis: Cobros duplicados por reintentos.**
   * **DESCARTADA.** El endpoint de cobro implementa `IdempotenciaService` con el encabezado `Idempotency-Key`, impidiendo la duplicación de transacciones en reintentos offline o de red.

---

## 11. Hallazgos de Seguridad

* **Total de Hallazgos:**
  * **CRÍTICO:** 0
  * **ALTO:** 0
  * **MEDIO:** 0
  * **BAJO:** 0
* **Conclusión:** No se detectaron vulnerabilidades críticas ni vías de escalada de privilegios o fuga de datos en la arquitectura de seguridad actual.
