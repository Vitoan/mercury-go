# Guía de Pruebas del Backend con Postman — MercuryGO

Esta guía contiene las consultas, headers, cuerpos JSON en `snake_case` y scripts de Postman para probar todos los flujos clave de `MercuryGo.Api` (tanto en local como en producción `https://mercurygo.runasp.net`), incluyendo el endpoint de reseteo de demo **`POST /api/admin/reset-demo`**.

---

## 1. Configuración Inicial en Postman (Variables de Entorno)

Creá un **Environment** en Postman (por ejemplo `MercuryGO - Prod`) con estas variables:

| Variable | Valor inicial | Descripción |
| :--- | :--- | :--- |
| `base_url` | `https://mercurygo.runasp.net` | URL base de la API (o `http://localhost:5000` si probás local) |
| `token_admin` | *(vacío)* | JWT del rol `ADMIN` |
| `token_cliente` | *(vacío)* | JWT del rol `CLIENTE` |
| `token_operario` | *(vacío)* | JWT del rol `OPERARIO` |
| `token_chofer` | *(vacío)* | JWT del rol `CHOFER` |
| `pedido_id` | *(vacío)* | ID del último pedido creado en las pruebas |

---

## 2. Healthcheck y Catálogo Público

### 2.1. Healthcheck de la API
- **Método / URL:** `GET {{base_url}}/health`
- **Auth:** No requiere.
- **Resultado esperado:** `200 OK` con `{"status": "ok", ...}`.

### 2.2. Listado público de productos
- **Método / URL:** `GET {{base_url}}/api/productos/listado`
- **Auth:** No requiere.
- **Resultado esperado:** `200 OK` con los productos y su `stock_actual`.

---

## 3. Autenticación por Rol (Login + Auto-guardado de Token)

En cada request de login, pegá en la pestaña **Scripts → Post-response** de Postman el fragmento correspondiente para guardar el JWT automáticamente en tu Environment.

### 3.1. Login ADMIN
- **Método / URL:** `POST {{base_url}}/api/sesion/login`
- **Headers:** `Content-Type: application/json`
- **Body (raw JSON):**
  ```json
  {
    "email": "admin@mercurygo.local",
    "password": "<TU_CLAVE_DEMO>"
  }
  ```
- **Script (Post-response):**
  ```javascript
  const json = pm.response.json();
  if (json.token) {
    pm.environment.set("token_admin", json.token);
  }
  ```

### 3.2. Login CLIENTE
- **Método / URL:** `POST {{base_url}}/api/sesion/login`
- **Body (raw JSON):**
  ```json
  {
    "email": "cliente@mercurygo.local",
    "password": "<TU_CLAVE_DEMO>"
  }
  ```
- **Script (Post-response):**
  ```javascript
  const json = pm.response.json();
  if (json.token) {
    pm.environment.set("token_cliente", json.token);
  }
  ```

### 3.3. Login OPERARIO
- **Método / URL:** `POST {{base_url}}/api/sesion/login`
- **Body (raw JSON):**
  ```json
  {
    "email": "operario@mercurygo.local",
    "password": "<TU_CLAVE_DEMO>"
  }
  ```
- **Script (Post-response):**
  ```javascript
  const json = pm.response.json();
  if (json.token) {
    pm.environment.set("token_operario", json.token);
  }
  ```

### 3.4. Login CHOFER
- **Método / URL:** `POST {{base_url}}/api/sesion/login`
- **Body (raw JSON):**
  ```json
  {
    "email": "chofer@mercurygo.local",
    "password": "<TU_CLAVE_DEMO>"
  }
  ```
- **Script (Post-response):**
  ```javascript
  const json = pm.response.json();
  if (json.token) {
    pm.environment.set("token_chofer", json.token);
  }
  ```

### 3.5. Ver perfil autenticado (`/yo`)
- **Método / URL:** `GET {{base_url}}/api/sesion/yo`
- **Auth:** Bearer Token `{{token_cliente}}` (o cualquier rol).
- **Resultado esperado:** `200 OK` con `id`, `nombre`, `email`, `rol_codigo`, `cliente_id`.

---

## 4. Flujo de Pedidos, Precios Oficiales e Idempotencia

### 4.1. Crear pedido como CLIENTE (con `Idempotency-Key`)
- **Método / URL:** `POST {{base_url}}/api/pedidos`
- **Auth:** Bearer Token `{{token_cliente}}`
- **Headers:**
  - `Content-Type: application/json`
  - `Idempotency-Key: 11111111-2222-4333-8444-555555555555` *(o usá `{{$guid}}` en Postman)*
- **Body (raw JSON):**
  ```json
  {
    "cliente_id": 1,
    "observaciones": "Prueba desde Postman - entregar por la tarde",
    "items": [
      { "producto_id": 1, "cantidad": 2 },
      { "producto_id": 2, "cantidad": 3 }
    ]
  }
  ```
- **Script (Post-response):**
  ```javascript
  const json = pm.response.json();
  if (json.id) {
    pm.environment.set("pedido_id", json.id);
  }
  ```
- **Prueba de Idempotencia:** Volvé a presionar **Send** con exactamente el mismo `Idempotency-Key`. Vas a comprobar que devuelve el mismo pedido sin descontar stock dos veces ni crear un duplicado.
- **Prueba de Seguridad de Precios:** Si enviás `"precio_unitario": 1` dentro de un ítem, el servidor igual lo ignora y congela `prod.Precio` de MySQL (`PedidosController.cs:311`).

### 4.2. Listar pedidos (Segmentación por rol)
- **Método / URL:** `GET {{base_url}}/api/pedidos`
- **Auth:** Bearer Token `{{token_cliente}}` (solo ve sus pedidos) o `{{token_admin}}` (ve todos).

### 4.3. Prueba Anti-IDOR (Cliente intentando ver pedido ajeno)
- **Método / URL:** `GET {{base_url}}/api/pedidos/2` *(el pedido 2 pertenece a `ClienteId = 2`, no al cliente demo 1)*
- **Auth:** Bearer Token `{{token_cliente}}`
- **Resultado esperado:** `403 Forbidden`.

---

## 5. Flujo Operario: Confirmación, Picking, Despacho y Remito PDF + QR

### 5.1. Confirmar el pedido recién creado (como ADMIN)
- **Método / URL:** `POST {{base_url}}/api/pedidos/{{pedido_id}}/confirmar`
- **Auth:** Bearer Token `{{token_admin}}`
- **Resultado esperado:** `200 OK`, estado pasa a `Confirmado`.

### 5.2. Hacer Picking en depósito (como OPERARIO)
- **Método / URL:** `PUT {{base_url}}/api/pedidos/{{pedido_id}}/picking`
- **Auth:** Bearer Token `{{token_operario}}`
- **Body (raw JSON):**
  ```json
  {
    "items": [
      { "producto_id": 1, "estado_item": "Completo", "cantidad_preparada": 2 },
      { "producto_id": 2, "estado_item": "Completo", "cantidad_preparada": 3 }
    ],
    "es_offline": false
  }
  ```

### 5.3. Asignar Chofer y Despachar (como OPERARIO o ADMIN)
- **Método / URL:** `PUT {{base_url}}/api/pedidos/{{pedido_id}}/chofer`
- **Auth:** Bearer Token `{{token_operario}}`
- **Body (raw JSON):**
  ```json
  {
    "chofer_id": 3
  }
  ```
- **Resultado esperado:** El pedido pasa a `Despachado` y queda asignado a `chofer@mercurygo.local` (`Id = 3`).

### 5.4. Descargar Remito PDF con QR
- **Método / URL:** `GET {{base_url}}/api/pedidos/{{pedido_id}}/comprobante`
- **Auth:** Bearer Token `{{token_operario}}` (o `admin`/`chofer`/`cliente` dueño)
- **Nota Postman:** Usá **Send and Download** para guardar el `.pdf` generado en memoria con QuestPDF y QRCoder.

---

## 6. Flujo Chofer: En Puerta (GPS), Entrega y Cobro

### 6.1. Marcar "En Puerta" con coordenadas GPS
- **Método / URL:** `PUT {{base_url}}/api/pedidos/{{pedido_id}}/estado`
- **Auth:** Bearer Token `{{token_chofer}}`
- **Headers:** `Idempotency-Key: {{$guid}}`
- **Body (raw JSON):**
  ```json
  {
    "estado": "EnPuerta",
    "latitud": -33.3017,
    "longitud": -66.3378,
    "es_offline": false
  }
  ```

### 6.2. Marcar "Entregado"
- **Método / URL:** `PUT {{base_url}}/api/pedidos/{{pedido_id}}/estado`
- **Auth:** Bearer Token `{{token_chofer}}`
- **Headers:** `Idempotency-Key: {{$guid}}`
- **Body (raw JSON):**
  ```json
  {
    "estado": "Entregado",
    "latitud": -33.3017,
    "longitud": -66.3378,
    "es_offline": false
  }
  ```

### 6.3. Registrar Cobro del Chofer
- **Método / URL:** `POST {{base_url}}/api/pedidos/{{pedido_id}}/cobro-chofer`
- **Auth:** Bearer Token `{{token_chofer}}`
- **Headers:** `Idempotency-Key: {{$guid}}`
- **Body (raw JSON):**
  ```json
  {
    "monto": 7650.00,
    "metodo": "Efectivo"
  }
  ```

---

## 7. Reset de Datos de la Demo (Endpoint Nuevo)

Cuando termines tus ensayos y quieras dejar la base de datos lista para repetir la demo o para el examen final:

### 7.1. Restablecer stock y pedidos de reserva (`PED-000002`, `PED-000003`, `PED-000005`)
- **Método / URL:** `POST {{base_url}}/api/admin/reset-demo`
- **Auth:** Bearer Token `{{token_admin}}` *(exclusivo para rol `ADMIN`; si probás con `{{token_cliente}}` devolverá `403 Forbidden`)*
- **Body:** *(vacío)*
- **Resultado esperado (`200 OK`):**
  ```json
  {
    "mensaje": "Datos de recorrido de demo restablecidos correctamente.",
    "productos_actualizados": 5,
    "pedidos_restaurados": [
      { "id": 2, "numero": "PED-000002", "estado": "Despachado", "cliente_id": 2, "chofer_id": 3 },
      { "id": 3, "numero": "PED-000003", "estado": "Confirmado", "cliente_id": 1, "chofer_id": null },
      { "id": 5, "numero": "PED-000005", "estado": "Pendiente", "cliente_id": 1, "chofer_id": null }
    ]
  }
  ```

> [!TIP]
> Para verificar qué pasa si alguien no autorizado intenta resetear la demo, ejecutá `POST {{base_url}}/api/admin/reset-demo` usando `{{token_cliente}}` o `{{token_chofer}}`: la API responderá **`403 Forbidden`**.
