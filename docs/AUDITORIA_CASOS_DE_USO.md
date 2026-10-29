# Auditoría de casos de uso — MercuryGO

**Fecha:** 2026-10-29
**Alcance:** contraste de los 23 casos de uso de `CASOS_DE_USO.md` contra el código real.
**Método:** lectura cruzada documento → controller → store → vista. Sin ejecución de la app ni modificación de código.
**Documento hermano:** `docs/AUDITORIA_FINAL.md` (auditoría técnica: arquitectura, rendimiento, seguridad).

> **Estado de resolución (2026-10-29)**
> - ✅ **P0-1** resuelto: el servidor resuelve el cliente desde la sesión y el carrito ya no tiene `cliente_id` fijo.
> - ✅ **P0-2** resuelto: se eliminaron los fallbacks al Cliente 1 en `Listar` y `cuenta-corriente`.
> - ✅ **P0-3** resuelto: el logout vacía el carrito y se agregó marca de dueño para cambios de usuario sin logout.
> - ✅ **P0-5** resuelto: el cobro valida estado, chofer asignado y tope contra el total; el cliente envía `Idempotency-Key` y `ocurrido_en`.
> - ✅ **P0-4** resuelto: el listado de pedidos filtra por `ChoferId` para el rol `CHOFER`.
> - ✅ **P1-7** resuelto: selector de chofer en el detalle, con `GET /api/pedidos/choferes`; despachar exige chofer asignado.
> - ✅ **P1-1** resuelto: la edición de pedido reconcilia líneas en su lugar y conserva precio histórico y cantidades de picking.
> - ✅ **P1-13** resuelto: la creación de pedido envía `Idempotency-Key`.
> - ✅ **P1-4** resuelto: cambios de estado, entrega fallida, entrega parcial y cobro envían `ocurrido_en`; la cola offline lo reenvía.

---

## 0. Leyenda

| Símbolo | Significado |
| :--- | :--- |
| ✅ | Implementado y coherente con el documento |
| 🟡 | Implementado con desvíos (funciona, pero no como se describe) |
| 🔴 | Roto o no implementado |

**Severidad:** `P0` bloqueante (datos, dinero, seguridad) · `P1` desvío funcional relevante · `P2` mejora o simplificación.

---

## 1. Resumen

| Estado | Cantidad | CU |
| :--- | :--- | :--- |
| ✅ Coherente | 6 | 04, 06, 15, 18, 19, 22 |
| 🟡 Con desvíos | 13 | 01, 02, 03, 05, 08, 09, 10, 11, 12, 13, 14, 17, 21 |
| 🔴 Roto / ausente | 4 | 07, 16, 20, 23 |

Hallazgos totales: **5 P0**, **13 P1**, **8 P2**.

---

## 2. P0 — Bloqueantes

### P0-1 · Todo pedido de autoservicio se crea para el Cliente 1

`MercuryGo.App/src/stores/carrito_store.js:21`

```js
const cliente_id = ref(1); // Por defecto cliente principal o se sincroniza con el usuario
```

Nunca se sincroniza. `confirmar_pedido()` (`:94`) envía `cliente_id: cliente_id_custom || cliente_id.value`, y **ningún llamador** pasa `cliente_id_custom`.

**Causa raíz:** `UsuarioResponse` (`SesionController.cs:20-27`) no expone `cliente_id`, y el JWT (`TokenService.Emitir:29-35`) no lleva claim de cliente. La app **no tiene forma** de conocer el cliente de la sesión, por eso hardcodea 1.

**Agravante:** `PedidosController.Crear:185-190` solo valida que el cliente exista y esté activo. No valida que un `CLIENTE` solo pueda crear para sí mismo; el `ClienteId` viene del body y el endpoint acepta los cuatro roles.

### P0-2 · Fuga de datos por fallback al Cliente 1

Dos endpoints caen al cliente `Id == 1` cuando el email del usuario no coincide con ningún cliente:

- `PedidosController.cs:40-44` → listado de pedidos.
- `PedidosController.cs:516-517` → cuenta corriente.

Un usuario `CLIENTE` sin registro comercial vinculado (o con email distinto al del cliente) ve los pedidos y el saldo de **Distribuidora El Norte**. Es el mismo antipatrón que P0-1, del lado del servidor.

### P0-3 · El logout no vacía el carrito

`CASOS_DE_USO.md:42` promete vaciarlo. `sesion_store.limpiar()` (`sesion_store.js:93-110`) resetea pedidos, borra copias, vacía la cola offline y borra la sesión, pero **no toca el carrito**. El carrito persiste en `localStorage['mercurygo_carrito']` (`carrito_store.js:8`) y sobrevive al cambio de usuario en el mismo dispositivo.

Escenario real: se entrega el teléfono a otra persona y esta ve el carrito anterior.

### P0-4 · Cada chofer ve la ruta de todos los choferes

`PedidosController.cs:55-62`

```csharp
else if (User.IsInRole(RolCodigos.Chofer))
{
    if (string.IsNullOrWhiteSpace(estados))
        consulta = consulta.Where(p => p.Estado == Despachado || p.Estado == EnPuerta);
}
```

Filtra por estado, no por `ChoferId`. Contradice `CASOS_DE_USO.md:110` ("ve solo los pedidos que tiene asignados"). Expone razón social y montos de toda la cartera.

> ✅ **Resuelto:** el listado agrega `p.ChoferId == User.UsuarioId()` para el rol `CHOFER` y el dispatch exige chofer asignado (`400 chofer_requerido`), por lo que no quedan pedidos huérfanos.

### P0-5 · Cobros duplicados e inconsistentes

`PedidosController.RegistrarCobroChofer:440-...` no valida:

- estado del pedido (acepta cobrar un `Pendiente` o un `Cancelado`);
- que el chofer autenticado sea el asignado;
- tope contra el total del pedido (`Monto` solo se valida `> 0`, `:456`).

La idempotencia por header (`:447-454`) funciona, pero `carrito_store.confirmar_pedido` y el modal de cobro no siempre envían `Idempotency-Key`. Un doble tap genera dos `CobroPedido` y el saldo del cliente queda mal.

---

## 3. P1 — Desvíos funcionales

| # | CU | Hallazgo | Evidencia |
| :--- | :--- | :--- | :--- |
| P1-1 | CU-08 | `ActualizarPedido` **borra y recrea** los `DetallePedido`. Se pierden `CantidadPedida`, `CantidadPreparada`, `CantidadRechazada`, `CantidadEntregada` y `MotivoRechazo`, y se re-congelan precios actuales. | `PedidosController.cs:407-424` — ✅ resuelto con `PedidoEdicionService` |
| P1-2 | CU-11 | El picking **no despacha**: `Confirmado → EnPreparacion`. El documento describe una sola acción que prepara y deja listo para despacho. | `PedidosController.cs:792-...`, `comp_picking_modal.vue:263` |
| P1-3 | CU-11 | Con faltantes **no se notifica al Admin**, pese a `CASOS_DE_USO.md:105`. Solo se genera notificación al cliente (`PedidoTransicionService:138`). | sin código |
| P1-4 | CU-16 | `ocurrido_en` se guarda en la cola pero **nunca se envía**. La API sí lo acepta (`CambiarEstadoRequest.OcurridoEn`, `CobroChoferRequest.OcurridoEn`); el faltante es 100 % del cliente. | `pedidos_service.js` `cambiar_estado` / `registrar_cobro_chofer` — ✅ resuelto |
| P1-5 | CU-16 | Conflictos no implementados: no hay "gana el chofer" explícito, no hay aviso al Admin, no se detecta pedido reasignado. Solo se marca la acción local como `conflicto`. | `offline_store.js:188-192` |
| P1-6 | CU-16 | `ActualizarPicking` **bloquea** el picking sobre `Cancelado`, contra la Decisión 16. | `PedidosController.cs:803` — ✅ resuelto: se guarda sin tocar importes |
| P1-7 | CU-20 | `asignar_chofer` existe en el service pero **no tiene UI**. En la práctica nadie asigna choferes, por lo que el filtro de P0-4 tampoco tendría datos. | `pedidos_service.js`, sin consumidor — ✅ resuelto con selector en el detalle y `GET /api/pedidos/choferes` |
| P1-8 | CU-21 | Falta la transición `(Despachado → NoEntregado)` en la matriz; `(NoEntregado → Despachado)` es solo Admin aunque la UI la ofrece al Chofer. | `PedidoTransicionService.cs:36-41` — ✅ resuelto |
| P1-9 | CU-05 | La API permite a `OPERARIO` crear/editar/eliminar clientes. Hoy es un ocultamiento de menú, no un control de acceso. | `ClientesController.cs:11` |
| P1-10 | CU-01 | `/api/sesion/login` **sin rate limiting ni lockout**. No hay `AddRateLimiter` en `Program.cs`. Fuerza bruta trivial. | `Program.cs`, `SesionController.cs:39-60` |
| P1-11 | CU-17 | No existe `PUT /api/usuarios/{id}/password`. El reseteo prometido no está implementado. | `UsuariosController.cs` |
| P1-12 | CU-23 | La entrega directa **no captura foto ni firma**; solo `EntregaParcial` las persiste. La prueba de entrega queda reducida a GPS. | `PedidosController.cs:1326-1327` |
| P1-13 | CU-07 | `confirmar_pedido()` llama `pedidos_service.crear(payload)` **sin** `Idempotency-Key`, aunque el service lo soporta. Doble tap = pedido duplicado. | `carrito_store.js:102` |

---

## 4. P2 — Mejoras y simplificaciones

1. **CU-03 / CU-11 — escaneo.** El escaneo de códigos **solo existe en `pedidos_page`** (`useEscaneoQr`, para remitos con QR). El documento lo atribuye al catálogo (`:48`) y al picking (`:103`). O se implementa `searchByBarcode`, o se corrige el documento.
2. **CU-04 — stock mínimo.** `Producto` **no tiene `StockActual` ni `StockMinimo`**. `:53` describe campos inexistentes (pendiente: decisión D2 + migración).
3. **CU-05 / CU-06 — campos comerciales.** `Cliente` **no tiene** condición de IVA ni límite de crédito. `:60`, `:65` y `:68` no son implementables sin migración.
4. **`CategoriasController`** (`GET /api/categorias`): sin consumidor. Las categorías ya vienen embebidas en `productos/resumen`.
5. **`DELETE /api/pedidos/{id}`**: sin uso en la app; la cancelación real va por `PUT /estado`. Candidato a eliminar.
6. **Doble camino de confirmación**: `POST /{id}/confirmar` y `PUT /{id}/estado` producen el mismo efecto. Conviene unificar.
7. **Regla transversal `:27` incumplida**: cobro, comprobante y picking escriben etiquetas en `Observaciones` en lugar de campos estructurados.
8. **Número de pedido**: `PED-{CountAsync + 1:D6}` (`PedidosController.cs:220-221`) colisiona bajo concurrencia. Usar secuencia o índice único con reintento.

---

## 5. Discrepancias documento ↔ código

| CU | Documento dice | Código real |
| :--- | :--- | :--- |
| 01 | `POST /api/sesion/refrescar`; `iniciar_sesion` / `cerrar_sesion` | `POST /api/sesion/refresh`; `entrar()` / `salir()` |
| 01 | "JWT con usuario, rol y cliente" | El JWT **no** incluye cliente (`TokenService.cs:29-43`) |
| 01 | Biometría "emite JWT" | Desbloquea un token local ya guardado |
| 03 | `GET /api/productos?buscar&por_pagina` | `GET /api/productos/listado?busqueda&tamano` (+ `categoria_id`, `disponible`, `en_oferta`, `pagina`) y `GET /api/productos/resumen` |
| 04 | `POST /api/productos/{id}/foto` | `POST /api/productos/{id}/imagen` |
| 05 | "condición de IVA, límite de crédito, saldo inicial 0" | No existen esos campos |
| 06 | `GET /api/clientes/{id}/cuenta-corriente`, `GET /api/pedidos/saldos-comerciales` | `GET /api/pedidos/cuenta-corriente`, `GET /api/pedidos/cuentas-corrientes-clientes` |
| 09 | `POST /api/pedidos/{id}/cancelar` | `PUT /api/pedidos/{id}/estado` (o `DELETE` sin uso) |
| 10 | Remito con **código de barras**; `GET /{id}/pdf` | QR (`GET /{id}/qr`); `GET /{id}/comprobante` |
| 14 | `POST /{id}/comprobante` | `POST /{id}/comprobante-pago` |
| 15 | `POST /aprobar-comprobante` | `PUT /aprobar-comprobante` |
| 16 | `encolar_operacion` / `sincronizar_pendientes` | `encolar_accion` / `sincronizar` |
| 17 | `PUT /api/usuarios/{id}/password` | No existe (sí `PUT /{id}/rol`) |
| 19 | `POST /{id}/confirmar` | Existe, pero la UI confirma por `PUT /estado` |
| 21 | `POST /{id}/reintentar-entrega` solo Admin | Correcto en API; la UI lo ofrece al Chofer |

---

## 6. Circuitos muertos o a medio construir

| Elemento | Estado |
| :--- | :--- |
| `CREAR_PEDIDO` en `offline_store` | La sincronización existe (`:290`); **nadie la alimenta** porque `confirmar_pedido()` no tiene rama offline |
| `asignar_chofer` | Service y endpoint sin UI |
| `CategoriasController` | Sin consumidor |
| `DELETE /api/pedidos/{id}` | Sin consumidor |
| `RetencionArchivosService` | ✅ Correcto: purga a 60 días, registrado en `Program.cs:106` |

---

## 7. Correcciones a la auditoría previa

- `EsOffline` **no** salta el control de roles: `PedidoTransicionService:84` se evalúa siempre. No es vulnerabilidad.
- `RetencionArchivosService` **sí** existe y está registrado. El hallazgo anterior que lo daba por ausente era incorrecto.
- `CambiarEstadoRequest` **sí** acepta `OcurridoEn`; el faltante de CU-16 es de frontend, no de contrato.

---

## 8. Orden de ataque sugerido

1. Identidad del cliente (P0-1 + P0-2): cierra la fuga entre clientes.
2. Cobros (P0-5): cierra el error de dinero.
3. Visibilidad del chofer (P0-4) + asignación (P1-7).
4. Logout limpio (P0-3).
5. Edición de pedidos (P1-1).
6. Migración de stock y crédito, y después CU-09 / CU-11 / CU-13 completos.
7. Push (D3) y `Cantidad` en picking (D4) siguen abiertos.

---

## 9. Decisiones abiertas

| # | Tema | Estado |
| :--- | :--- | :--- |
| D1 | Entidad `PagosCuentaCorriente` | ✅ Aprobada, requiere migración |
| D2 | Stock real (`StockActual` / `StockMinimo`) | ✅ Aprobada, requiere migración |
| D2b | Retirar la promesa de stock del documento mientras no esté desplegado | ✅ Aprobada |
| D3 | Push notifications | ⏳ Sin decisión |
| D4 | `Cantidad` en picking | ⏳ Sin decisión |
| D5 | Informe de auditoría | ✅ Aprobado |
