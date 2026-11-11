# Stage 9: Implementation Readiness Test Report

*   **Target Document:** `auditoria/05-plan-remediacion-criticos.md`
*   **Reviewers:** `egm-implementation-readiness`, `egm-codebase-details`, `egm-style`
*   **Scope:** `DetailedRange` (lines 168..271)
*   **Date:** 2026-11-10

--------------------------------------------------------------------------------

## 1. Codebase Alignment Audit (`egm-codebase-details`)

### Archivos y Símbolos Faltantes en el Plan
*   **🔴 Omisión Crítica de `TokenService.cs`:**
    *   *Evidencia en código real:* `MercuryGo.Api/Auth/TokenService.cs:29-43` emite claims para `sub`, `email`, `name`, `jti` y `rol_codigo`, pero **NO incluye** el claim `cliente_id`.
    *   *Consecuencia:* Si el plan propone filtrar en `PedidosController.cs` mediante `User.ObtenerClienteId()`, el claim no existirá en el token JWT y el filtro fallará en tiempo de ejecución.
    *   *Acción obligatoria:* Incorporar `MercuryGo.Api/Auth/TokenService.cs` a la lista de archivos a modificar para agregar el claim `cliente_id` cuando `usuario.ClienteId.HasValue`.

*   **🔴 Omisión Crítica de `ClaimsExtensions.cs`:**
    *   *Evidencia en código real:* `MercuryGo.Api/Auth/ClaimsExtensions.cs` define `UsuarioId()`, `RolCodigo()`, `EsAdmin()`, `EsCliente()`, `EsOperario()`, `Email()`, pero **NO tiene implementado** ningún método `ObtenerClienteId()` ni `ClienteId()`.
    *   *Consecuencia:* El código propuesto no compilará (`CS1061: 'ClaimsPrincipal' does not contain a definition for 'ObtenerClienteId'`).
    *   *Acción obligatoria:* Incorporar `MercuryGo.Api/Auth/ClaimsExtensions.cs` a la lista de archivos a modificar agregando el método de extensión:
        ```csharp
        public static long? ClienteId(this ClaimsPrincipal usuario)
        {
            var valor = usuario.FindFirstValue("cliente_id");
            return long.TryParse(valor, out var id) ? id : null;
        }
        ```

## 2. Implementation Ambiguity & Readiness (`egm-implementation-readiness`)

### Validación de Producto Inexistente en `PedidosController.cs`
*   *Ambigüedad:* Si un cliente envía un `ProductoId` inexistente o dado de baja, el plan dice "busca el precio oficial", pero no define si lanza excepción o retorna un HTTP 400.
*   *Instrucción ejecutable:* Si `_context.Productos.FindAsync(detalle.ProductoId)` retorna `null` o `!producto.Activo`, responder inmediatamente `BadRequest(new { mensaje = $"El producto ID {detalle.ProductoId} no existe o no está activo." })`.

### Máquina de Estados de la Cola en `offline_store.js`
*   *Ambigüedad:* Cuando una operación offline da 409, el plan dice "se registra como conflictiva". Debe indicarse explícitamente el campo y valor en el objeto de la cola.
*   *Instrucción ejecutable:* Asignar `item.estado = 'conflicto'` e `item.mensaje_error = error.response?.data?.mensaje || 'Conflicto de sincronización'`, persistiendo el cambio en localforage y continuando con el siguiente elemento.

--------------------------------------------------------------------------------

## Findings Summary

| ID | Persona | Severidad | Hallazgo | Acción Obligatoria |
| :--- | :--- | :--- | :--- | :--- |
| IR-01 | egm-codebase-details | 🔴 Blocker | Falta `TokenService.cs` en lista de archivos a modificar | Modificar `TokenService.cs` para emitir el claim `cliente_id` en el JWT. |
| IR-02 | egm-codebase-details | 🔴 Blocker | Falta `ClaimsExtensions.cs` en lista de archivos a modificar | Agregar método de extensión `ClienteId(this ClaimsPrincipal)` en `ClaimsExtensions.cs`. |
| IR-03 | egm-implementation-readiness | 🟡 Major | Falta validación explícita de `Producto == null` en recálculo | Agregar retorno `BadRequest` descriptivo si el producto no existe en el catálogo. |
| IR-04 | egm-implementation-readiness | 🟢 Nit | Especificar campos de estado para cola en `offline_store.js` | Establecer `item.estado = 'conflicto'` e `item.mensaje_error`. |
