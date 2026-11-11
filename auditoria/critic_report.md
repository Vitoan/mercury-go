# Stage 7: Critic Review Report

*   **Target Document:** `auditoria/05-plan-remediacion-criticos.md`
*   **Reviewers:** `egm-design`, `egm-structural`, `egm-style`
*   **Scope:** `FeatureRange` (lines 1..167)
*   **Date:** 2026-11-10

--------------------------------------------------------------------------------

## 1. Design & Failure Mode Critique (`egm-design`)

### Concurrencia y Sincronización Offline
*   **Riesgo de condición de carrera en stock:** Si dos choferes entregan o cobran pedidos simultáneos para el mismo cliente o producto en offline, ¿el recálculo autoritativo valida la disponibilidad de stock al momento de la sincronización?
*   *Recomendación:* `PedidoEdicionService` debe encapsular la verificación de stock dentro de una transacción serializable o atómica con bloqueo optimista.

### Expiración de Token durante Sincronización de Cola
*   **Falla transitoria HTTP 401 en medio de la cola:** Si el token JWT expira a mitad de la sincronización de 5 operaciones offline, la primera operación fallará con 401. Si el interceptor de Axios renueva el token, la cola debe reintentar esa misma operación con el nuevo token sin marcarla erróneamente como "conflicto permanente".

## 2. Structural & Layout Validation (`egm-structural`)

*   **Sintaxis y Encabezados:** Estructura limpia y simétrica. Cumple la plantilla EGM con secciones Overview, Problem, Goals, Non-Goals, Open Questions, CUJs, Technical Plan, y Alternatives.
*   **CUJs (Given/When/Then):** Los 3 CUJs tienen precondiciones, acciones y resultados esperados claros y testeables.

## 3. Style & Copy Audit (`egm-style`)

*   **Tono y Precisión:** Redacción sobria y técnica. Sin uso de adjetivos promocionales vacíos (*seamless*, *blazingly fast*).
*   **Observación:** En la sección "The Problem", simplificar la numeración redundante para mantener la lectura directa.

--------------------------------------------------------------------------------

## Findings Summary

| ID | Persona | Severidad | Hallazgo | Recomendación |
| :--- | :--- | :--- | :--- | :--- |
| CRIT-01 | egm-design | 🔴 Blocker | Manejo de HTTP 401 en bucle offline | Diferenciar explícitamente errores de autenticación (401) de conflictos de dominio (409/400). El 401 debe pausar la cola y forzar refresh de token, no marcar la operación como descartada. |
| CRIT-02 | egm-design | 🟡 Major | Bloqueo o validación de stock en edición | Si un producto se queda sin stock mientras el pedido estaba en borrador, definir si el recálculo retorna error o ajusta unidades. |
| CRIT-03 | egm-style | 🟢 Nit | Formato de nombres de subsistemas | Estandarizar la titulación de los subsistemas bajo Technical Plan con estilo nominal conciso. |
