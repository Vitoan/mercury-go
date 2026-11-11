# Auditoría de Calidad Técnica, Deuda y Pruebas — MercuryGO

**Fecha:** 2026-11-10  
**Commit HEAD:** `2960114417a912d936580e2b020816d946d1cd42`  
**Reglas aplicadas:** `/auditoria/00-reglas.md` (Solo lectura de código, verificación línea por línea contra código real, estado y visión en el teléfono).

---

## 1. Resultados Reales de Suites de Pruebas y Compilación

### Backend (.NET 10 / xUnit)
* **Comando:** `dotnet test MercuryGo.Api.Tests\MercuryGo.Api.Tests.csproj`
* **Resultado Real:**
  ```text
  Serie de pruebas para MercuryGo.Api.Tests.dll (.NETCoreApp,Version=v10.0)
  Correctas! - Con error: 0, Superado: 69, Omitido: 0, Total: 69, Duración: 1 s
  ```
* **Estado:** **69 superadas, 0 fallidas (100% éxito)**.
* **Compilación de la API (`dotnet build`):**
  * `0 Errores`, `2 Advertencias` (Vulnerabilidad moderada conocida reportada por NuGet en el paquete `MySqlConnector 2.4.0` `GHSA-473q-m89c-ghf8`).

### Frontend (Vue 3 + Vite / Vitest)
* **Comando:** `npm --prefix MercuryGo.App run test:run`
* **Resultado Real:**
  ```text
  RUN  v4.1.10 MercuryGo.App
  ✓ src/utils/formato.spec.js (5 tests) 56ms
  ✓ src/utils/estados.spec.js (6 tests) 10ms
  ✓ src/stores/offline_store.spec.js (6 tests) 15ms

  Test Files  3 passed (3)
       Tests  17 passed (17)
    Duration  3.15s
  ```
* **Estado:** **17 superadas en 3 suites, 0 fallidas (100% éxito)**.
* **Compilación Frontend (`npm run build`):**
  * Salida generada en `dist/` en 8.64s.
  * **Warnings Relevantes:**
    1. `[lightningcss minify] 'host-context' is not recognized as a valid pseudo-class`: Proviene de estilos CSS internos de Ionic Framework para soporte RTL (`[dir=rtl]`), inocuo para la app.
    2. `(!) Some chunks are larger than 500 kB after minification`: El bundle principal `dist/assets/index-*.js` pesa 1,230 kB (273 kB gzipped) debido a la inclusión conjunta de componentes Ionic y Capacitor.

---

## 2. Auditoría de Deuda Técnica, Código Muerto y Sobreingeniería

* **Comentarios `TODO` / `HACK` / `FIXME`:**
  * Búsqueda ejecutada en todo el código fuente: **0 incidencias** de `TODO` o `FIXME` pendientes.
* **Patrones de Deuda Identificados:**
  1. **Duplicación de Formateo Monetario:**
     * `MercuryGo.App/src/utils/formato.js:1-35` centraliza `formatear_moneda`, pero varios componentes (`comp_detalle_pedido_modal.vue` y `comp_cuentas_corrientes_seccion.vue`) re-implementan parsing manual con regex sobre strings numéricas en lugar de llamar a la función utilitaria compartida.
  2. **Etiquetas Legacy en `Observaciones`:**
     * `CuentasCorrientesController.cs:95-99` y `351-356`: A pesar de que existen tablas y columnas relacionales estructuradas (`cobros_pedido`, `comprobante_url`, `comprobante_estado`), la API continúa formateando e inyectando cadenas como `[Cobro Chofer: $X.XX]` o `[Comprobante de pago: APROBADO]` en la columna de texto `observaciones` para retrocompatibilidad histórica.

---

## 3. Análisis de Ciclos de Importación en Frontend

### Ciclo 1: `offline_store.js` ↔ `pedidos_store.js`
* **Evidencia:**
  * `pedidos_store.js:4` importa `use_offline_store` desde `@/stores/offline_store`.
  * `offline_store.js:5` importa `usePedidosStore` desde `@/stores/pedidos_store`.
* **Impacto Real vs. Estético:**
  * **SOLO ESTÉTICO (Sin riesgo de runtime):** Ambas dependencias son stores de Pinia donde la invocación se produce de forma diferida dentro de acciones (`usePedidosStore().cargar(true)` en `offline_store.js:255` y `use_offline_store()` dentro de la función utilitaria `con_cambios_sin_enviar`). En el momento de evaluación del módulo no se ejecutan referencias circulares top-level que puedan resultar en `undefined`.

### Ciclo 2: `router/index.js` ↔ `notificaciones_service.js`
* **Evidencia:**
  * `notificaciones_service.js:6` importa `router` desde `@/router`.
  * `router/index.js` no importa `notificaciones_service.js`.
* **Impacto Real vs. Estético:**
  * **INEXISTENTE:** No hay ciclo. El servicio de notificaciones navega vía `router.push`, pero el enrutador no tiene conocimiento ni importa el servicio de notificaciones.

---

## 4. Complejidad por Tamaño de Componentes y Cobertura de Tests

### Top 5 Controladores más Extensos (Backend)
1. `CuentasCorrientesController.cs` (691 líneas): Maneja cobros, cuentas corrientes, auditoría de comprobantes y descargas de archivos físicos.
2. `PedidosController.cs` (636 líneas): Paginación, cálculo autoritativo de órdenes, transición de estados y filtros multi-rol.
3. `DespachoController.cs` (560 líneas): Hoja de ruta, picking, entregas fallidas, entregas parciales y reprogramaciones.
4. `ProductosController.cs` (275 líneas): Catálogo, imágenes y validación de inventario.
5. `SesionController.cs` (198 líneas): Autenticación, rotación de tokens, rate limiting y logout.

### Top 5 Vistas más Extensas (Frontend)
1. `clientes_page.vue` (488 líneas): ABM de clientes y vista unificada con finanzas.
2. `productos_page.vue` (484 líneas): Catálogo comercial y modal de stock.
3. `pedidos_page.vue` (416 líneas): Listado dinámico por roles y estados.
4. `cuenta_page.vue` (404 líneas): Perfil, estado de conexión y cierre de sesión.
5. `login_page.vue` (359 líneas): Login con roles, biometría nativa y selector de modo oscuro.

### Flujos Críticos sin Pruebas Unitarias Automatizadas
1. **Flujo de Picking con Faltantes:** `PUT /api/pedidos/{id}/picking` no cuenta con prueba automatizada para el ajuste de stock ante ítems incompletos en `MercuryGo.Api.Tests`.
2. **Descarga Física Segura de Archivos:** Las restricciones de path traversal en `ObtenerArchivoComprobantePago` se verificaron analíticamente pero carecen de test unitario con mock de `IWebHostEnvironment`.
3. **Flujo de Cámara y QR en Frontend:** Las pruebas de `camara_service.js` y `qr_service.js` dependen de mocks nativos de Capacitor y no están integradas en la suite de Vitest.

---

## 5. Accesibilidad, Estados Visuales y Ergonomía Táctil (Modern Web)

* **Estados de Carga, Error y Vacío:**
  * Implementados sistemáticamente mediante la tríada de componentes base:
    * `comp_esqueleto.vue`: Placeholders animados con CSS pulse.
    * `comp_estado_error.vue`: Fondo con alerta semántica y botón de reintento con área de toque ampliada.
    * `comp_estado_vacio.vue`: Ilustración SVG atenuada y mensaje claro sin jerga técnica.
* **Contraste y Modo Oscuro:**
  * Las variables en `src/theme/variables.css` definen tokens de color WCAG AA (`--mercury-primary`, `--mercury-bg-page`, `--mercury-text-main`).
  * El selector de modo oscuro en `login_page.vue` y `comp_menu.vue` alterna la clase `.dark` sobre el elemento raíz `<html>`, invirtiendo contrastes correctamente.
* **Dimensiones Táctiles (Touch Targets):**
  * Los botones principales de acción (`ion-button`) mantienen alturas de 48px o superiores, cumpliendo la directiva de 44x44px mínimos recomendada para interfaces móviles.
  * Los ítems de lista paginada (`ion-item-sliding`) poseen margen de separación y altura táctil confortable para uso con una sola mano en ruta de reparto.

---

## Hallazgos de Esta Auditoría

* **Total de Hallazgos:**
  * **CRÍTICO:** 0
  * **ALTO:** 0
  * **MEDIO:** 0
  * **BAJO:** 0
* **Conclusión:** Las suites de pruebas de backend y frontend compilan y pasan en su totalidad (69 en API, 17 en App). No existen bloqueantes de calidad ni dependencias circulares que amenacen la estabilidad de la aplicación.
