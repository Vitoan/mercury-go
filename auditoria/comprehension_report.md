# Stage 6: Comprehension Test Report

*   **Target Document:** `auditoria/05-plan-remediacion-criticos.md`
*   **Reviewer:** `egm-comprehension`
*   **Scope:** `FeatureRange` (lines 1..167) & `DetailedRange` (lines 168..271)
*   **Date:** 2026-11-10

--------------------------------------------------------------------------------

## Problem Paraphrase

MercuryGO tiene fallas técnicas que impiden defender con éxito el proyecto final:
1. El sistema permite que el teléfono del cliente defina el precio y el total a cobrar en un pedido, permitiendo compras fraudulentas.
2. Las notificaciones automáticas al celular están apagadas por configuración y la API no tiene la clave de Firebase lista para enviar alertas.
3. Clientes y usuarios pueden consultar o modificar información que pertenece a otras personas porque los puntos de acceso de la API no comprueban estrictamente quién está llamando.
4. Si un chofer pierde la conexión y cobra un pedido que fue cancelado antes, toda la cola de cobranzas posteriores se traba y no se envía nada más.
5. El cierre de sesión no borra los datos confidenciales guardados en la memoria del teléfono.

## Solution Paraphrase

1. Cuando llega un pedido, la API busca el precio oficial en su propia base de datos, multiplica por la cantidad y calcula el total final por su cuenta, sin importar lo que haya mandado la aplicación.
2. Se activa el interruptor de notificaciones push en los archivos de configuración y se asegura que la clave de Firebase se copie automáticamente al publicar la API en la nube.
3. Se añaden filtros obligatorios para que los clientes solo vean sus propios pedidos y cuentas, y se exige rol de administrador o chofer según corresponda en cada operación.
4. La cola de operaciones sin conexión ahora marca el pedido conflictivo como fallido pero continúa enviando todos los demás cobros normales.
5. Al pulsar "Cerrar sesión", la app vacía por completo el carrito, los pedidos en memoria y los tokens de acceso guardados en el almacenamiento seguro del teléfono.

--------------------------------------------------------------------------------

## Comprehension Findings

| ID | Severidad | Término / Concepto | Observación |
| :--- | :--- | :--- | :--- |
| COMP-01 | 🟡 Major | Claim `cliente_id` | El documento asume que el token JWT ya contiene el claim `cliente_id`, pero el lector no sabe dónde se genera este claim si el usuario inicia sesión. Debe aclararse explícitamente cómo se asocia un usuario con su cliente. |
| COMP-02 | 🟢 Nit | Acrónimo "RBAC" | No se define formalmente en el texto inicial ("Role-Based Access Control / Control de acceso basado en roles"). |
