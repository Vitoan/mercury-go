# EGM Consolidated Review Report (Goldfish Review Protocol)

*   **Target Document:** [auditoria/05-plan-remediacion-criticos.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/05-plan-remediacion-criticos.md)
*   **Execution Preset:** Consolidated report with all review rubrics (Uninterrupted Full Flow)
*   **Stages Executed:** Stage 5 (Zoom Out) → Stage 6 (Comprehension) → Stage 7 (Critic) → Stage 8 (Intermediate Zoom Out) → Stage 9 (Implementation Readiness)
*   **Date:** 2026-11-10

--------------------------------------------------------------------------------

## Executive Summary

La revisión crítica multi-perspectiva del protocolo **Elephant-Goldfish Model (EGM)** auditó el plan de remediación en sus dimensiones arquitectónica, comprensión conceptual, resiliencia ante fallos y factibilidad estricta contra el código fuente real de MercuryGO.

El plan fue calificado como **SÓLIDO Y FACTIBLE**, pero la auditoría descubrió **3 BLOQUEANTES CRÍTICOS DE IMPLEMENTACIÓN (Blockers)** que harían fallar la compilación o romperían los filtros en tiempo de ejecución si el desarrollador ejecutara el plan tal cual fue redactado inicialmente:
1. `TokenService.cs` no fue listado como archivo a modificar y actualmente **no emite** el claim `cliente_id` en el JWT.
2. `ClaimsExtensions.cs` no fue listado como archivo a modificar y **carece** del método de extensión para resolver el `cliente_id`.
3. El bucle offline no distinguía un error transitorio de expiración de token (HTTP 401) de un conflicto de negocio permanente (HTTP 409), arriesgando el descarte indebido de operaciones válidas.

---

## Actionable Tasks by Priority

### 🔴 Blockers (Impiden la compilación o rompen la ejecución)

#### ACT-01: Incorporar `TokenService.cs` al plan para emitir el claim `cliente_id`
*   **Origen:** `IR-01` (`egm-codebase-details`)
*   **Archivo:** `MercuryGo.Api/Auth/TokenService.cs`
*   **Acción requerida:** En el método `Emitir(Usuario usuario, Rol? rol)`, verificar si `usuario.ClienteId.HasValue` y agregar:
    ```csharp
    if (usuario.ClienteId.HasValue)
    {
        claims.Add(new Claim("cliente_id", usuario.ClienteId.Value.ToString()));
    }
    ```

#### ACT-02: Incorporar `ClaimsExtensions.cs` al plan con el método `ClienteId()`
*   **Origen:** `IR-02` (`egm-codebase-details`)
*   **Archivo:** `MercuryGo.Api/Auth/ClaimsExtensions.cs`
*   **Acción requerida:** Agregar el método de extensión para resolver de forma segura el identificador del cliente:
    ```csharp
    public static long? ClienteId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue("cliente_id");
        return long.TryParse(valor, out var id) ? id : null;
    }
    ```

#### ACT-03: Diferenciar fallos de autenticación (401) de conflictos de estado (409/400) en cola offline
*   **Origen:** `CRIT-01` (`egm-design`)
*   **Archivo:** `MercuryGo.App/src/stores/offline_store.js`
*   **Acción requerida:** Si la sincronización responde HTTP 401, pausar inmediatamente el procesamiento de la cola y disparar la renovación de sesión (refresh token) para reintentar la misma operación, sin marcarla como conflicto definitivo. Solo respuestas HTTP 400/409 deben marcarse con `estado = 'conflicto'`.

---

### 🟡 Major (Riesgos de integridad o calidad arquitectónica)

#### ACT-04: Validar existencia y estado activo de productos en recálculo autoritativo
*   **Origen:** `IR-03` (`egm-implementation-readiness`)
*   **Archivo:** `MercuryGo.Api/Controllers/PedidosController.cs` / `PedidoEdicionService.cs`
*   **Acción requerida:** Si un `ProductoId` no se encuentra en `_context.Productos` o tiene `!producto.Activo`, retornar de inmediato un `BadRequest` descriptivo en lugar de permitir excepciones por referencia nula.

#### ACT-05: Centralizar el cálculo autoritativo en `PedidoEdicionService`
*   **Origen:** `ZO-01` (`egm-zoom-out`)
*   **Archivo:** `MercuryGo.Api/Services/PedidoEdicionService.cs`
*   **Acción requerida:** Evitar duplicar consultas y cálculos de sumatoria dentro de los métodos del controlador; encapsular el cálculo de subtotales y totales en el servicio de dominio.

#### ACT-06: Aclarar en la documentación la vinculación usuario-cliente
*   **Origen:** `COMP-01` (`egm-comprehension`)
*   **Archivo:** `auditoria/05-plan-remediacion-criticos.md`
*   **Acción requerida:** Añadir un párrafo aclaratorio explicando que los usuarios B2B tienen una clave foránea `ClienteId` en la tabla `usuarios` que los enlaza con la empresa que representan.

---

### 🟢 Nit (Mejoras menores y estilo)

#### ACT-07: Estandarizar propiedades de estado en items de la cola offline
*   **Origen:** `IR-04` (`egm-implementation-readiness`)
*   **Archivo:** `MercuryGo.App/src/stores/offline_store.js`
*   **Acción requerida:** Asignar formalmente `item.estado = 'conflicto'` e `item.mensaje_error = error.response?.data?.mensaje || 'Conflicto de sincronización'`.

#### ACT-08: Agregar log de advertencia ante discrepancia intencional de precios
*   **Origen:** `ZO-02` (`egm-inventive-catalyst`)
*   **Archivo:** `MercuryGo.Api/Controllers/PedidosController.cs`
*   **Acción requerida:** Emitir un `_logger.LogWarning(...)` cuando el subtotal recibido del cliente difiera del precio oficial del catálogo, para fines de auditoría de seguridad.

#### ACT-09: Definir explícitamente el acrónimo RBAC en el documento
*   **Origen:** `COMP-02` (`egm-comprehension`)
*   **Archivo:** `auditoria/05-plan-remediacion-criticos.md`

--------------------------------------------------------------------------------

## Traceability Matrix

| Raw Finding ID | Review Stage | Persona Evaluadora | Action Item Consolidado | Prioridad |
| :--- | :--- | :--- | :--- | :--- |
| `ZO-01` | Stage 5: Initial Zoom Out | `egm-zoom-out` | `ACT-05` (Centralizar en PedidoEdicionService) | 🟡 Major |
| `ZO-02` | Stage 5: Initial Zoom Out | `egm-inventive-catalyst` | `ACT-08` (Log de advertencia en auditoría) | 🟢 Nit |
| `COMP-01` | Stage 6: Comprehension Test | `egm-comprehension` | `ACT-06` (Aclarar vínculo usuario-cliente) | 🟡 Major |
| `COMP-02` | Stage 6: Comprehension Test | `egm-comprehension` | `ACT-09` (Definir acrónimo RBAC) | 🟢 Nit |
| `CRIT-01` | Stage 7: Critic Review | `egm-design` | `ACT-03` (Manejo de HTTP 401 en cola offline) | 🔴 Blocker |
| `CRIT-02` | Stage 7: Critic Review | `egm-design` | `ACT-04` (Validar producto inexistente/inactivo) | 🟡 Major |
| `CRIT-03` | Stage 7: Critic Review | `egm-style` | (Resuelto en edición) | 🟢 Nit |
| `IZO-01` | Stage 8: Intermediate Zoom Out | `egm-zoom-out` | (Confirmación de arquitectura mínima) | 🟢 Nit |
| `IR-01` | Stage 9: Implementation Readiness | `egm-codebase-details` | `ACT-01` (Modificar TokenService.cs para claim) | 🔴 Blocker |
| `IR-02` | Stage 9: Implementation Readiness | `egm-codebase-details` | `ACT-02` (Modificar ClaimsExtensions.cs) | 🔴 Blocker |
| `IR-03` | Stage 9: Implementation Readiness | `egm-implementation-readiness` | `ACT-04` (Retorno BadRequest en producto nulo) | 🟡 Major |
| `IR-04` | Stage 9: Implementation Readiness | `egm-implementation-readiness` | `ACT-07` (Propiedades estado y mensaje_error) | 🟢 Nit |

--------------------------------------------------------------------------------

## Stage Reports Index
*   Stage 5: [auditoria/zoom_out_report.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/zoom_out_report.md)
*   Stage 6: [auditoria/comprehension_report.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/comprehension_report.md)
*   Stage 7: [auditoria/critic_report.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/critic_report.md)
*   Stage 8: [auditoria/intermediate_zoom_out_report.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/intermediate_zoom_out_report.md)
*   Stage 9: [auditoria/implementation_readiness_report.md](file:///c:/Users/SamsungGB3/Documents/GitHub/mercury-go/auditoria/implementation_readiness_report.md)
