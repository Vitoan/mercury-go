# Stage 5: Initial Zoom Out Review Report

*   **Target Document:** `auditoria/05-plan-remediacion-criticos.md`
*   **Reviewers:** `egm-zoom-out`, `egm-inventive-catalyst`
*   **Scope:** Full File
*   **Date:** 2026-11-10

--------------------------------------------------------------------------------

## 1. High-Altitude Architectural Assessment (`egm-zoom-out`)

### Simplicity & YAGNI
*   **Veredicto:** El alcance está fuertemente acotado al objetivo académico de la defensa (M01–M09). No introduce abstracciones prematuras ni sobreingeniería (por ejemplo, no se agregan middlewares custom innecesarios ni microservicios externos).
*   **Alineación con el código existente:** Utiliza los servicios ya instanciados en el contenedor de dependencias (`EnvioPush`, `IdempotenciaService`, `ConflictoOfflineService`).
*   **Observación de Reutilización:** El recálculo de precios unitarios y subtotales debe centralizarse preferentemente en `PedidoEdicionService` en lugar de duplicar lógica ad-hoc dentro de `PedidosController.cs`.

## 2. TRIZ Contradiction Analysis (`egm-inventive-catalyst`)

### Contradicción Principal 1: Seguridad vs. Fluidez de Usuario (Precios de Pedido)
*   *Tensión:* Rechazar pedidos con precios alterados con HTTP 400 protege la integridad financiera, pero arriesga falsos positivos por discrepancias de centavos (redondeo JS vs C#) en la demo en vivo.
*   *Resolución EGM:* Principio TRIZ #10 (Acción Preliminar / Autoritativa). El servidor ignora el campo enviado y calcula el precio oficial desde la base de datos. Se elimina la vulnerabilidad sin penalizar al usuario legítimo ni generar fricción.

### Contradicción Principal 2: Disponibilidad Offline vs. Consistencia Relacional
*   *Tensión:* Permitir cobros offline genera riesgo de registrar entregas sobre pedidos cancelados previamente en la central.
*   *Resolución EGM:* Principio TRIZ #24 (Intermediario / Aislamiento). La cola offline aísla la transacción en conflicto (marcada como observada con HTTP 409) sin frenar en seco la sincronización del lote restante.

--------------------------------------------------------------------------------

## Findings Summary

| ID | Persona | Severidad | Hallazgo | Recomendación |
| :--- | :--- | :--- | :--- | :--- |
| ZO-01 | egm-zoom-out | 🟡 Major | Posible duplicación de lógica de precios en controlador | Delegar el cálculo autoritativo a `PedidoEdicionService` para no sobrecargar `PedidosController`. |
| ZO-02 | egm-inventive-catalyst | 🟢 Nit | Falta telemetría de auditoría ante discrepancia de precio | Registrar en el log de la API un warning cuando el precio enviado por el cliente difiera del precio oficial del catálogo. |
