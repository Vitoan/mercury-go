using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario},{RolCodigos.Chofer},{RolCodigos.Cliente}")]
[Route("api/pedidos")]
public sealed class PedidosController(
    MercuryGoDbContext db,
    MercuryGo.Api.Services.IPedidoTransicionService transicionService,
    MercuryGo.Api.Services.IdempotenciaService idempotenciaService,
    MercuryGo.Api.Services.ConflictoOfflineService conflictoOfflineService) : PedidosControllerBase(db)
{
    // Estados en los que todavía se puede corregir la mercadería del pedido.
    private static readonly EstadoPedido[] EstadosConItemsEditables =
    [
        EstadoPedido.Pendiente,
        EstadoPedido.Confirmado,
        EstadoPedido.EnPreparacion
    ];

    // Listado paginado con filtros de texto, estados múltiples ("Pendiente,EnPreparacion") y cliente.
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? estados,
        [FromQuery] long? cliente_id,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken ct)
    {
        var consulta = db.Pedidos.AsNoTracking().Where(p => p.Activo);

        // Segmentación de pedidos según rol del usuario autenticado (Usuario.ClienteId y email)
        if (User.EsCliente())
        {
            var propio = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            consulta = propio != null
                ? consulta.Where(p => p.ClienteId == propio.Id)
                : consulta.Where(p => false);
        }
        else if (User.EsOperario())
        {
            // Operario de depósito: se enfoca en órdenes confirmadas y en preparación
            if (string.IsNullOrWhiteSpace(estados))
            {
                consulta = consulta.Where(p => p.Estado == EstadoPedido.Confirmado || p.Estado == EstadoPedido.EnPreparacion);
            }
        }
        else if (User.IsInRole(RolCodigos.Chofer))
        {
            // Cada chofer ve solo su propia ruta.
            var choferId = User.UsuarioId();
            consulta = consulta.Where(p => p.ChoferId == choferId);

            if (string.IsNullOrWhiteSpace(estados))
            {
                consulta = consulta.Where(p => p.Estado == EstadoPedido.Despachado || p.Estado == EstadoPedido.EnPuerta);
            }
        }

        var texto = (busqueda ?? string.Empty).Trim();
        if (texto.Length > 0)
        {
            consulta = consulta.Where(p => p.Numero.Contains(texto) || p.Cliente!.RazonSocial.Contains(texto));
        }

        if (cliente_id.HasValue && cliente_id.Value > 0)
        {
            consulta = consulta.Where(p => p.ClienteId == cliente_id.Value);
        }

        var listaEstados = FiltroEnum.Parsear<EstadoPedido>(estados);
        if (listaEstados.Count > 0)
        {
            consulta = consulta.Where(p => listaEstados.Contains(p.Estado));
        }

        var (items, paginaResponse) = await (
            from pedido in consulta
            join cliente in db.Clientes.AsNoTracking() on pedido.ClienteId equals cliente.Id
            orderby pedido.FechaPedido descending
            select new PedidoListaResponse(
                pedido.Id,
                pedido.Numero,
                pedido.Estado.ToString(),
                pedido.FechaPedido,
                cliente.Id,
                cliente.RazonSocial,
                Math.Round(
                    db.DetallesPedido
                        .Where(d => d.PedidoId == pedido.Id)
                        .Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                    2),
                pedido.Observaciones
            )
        ).PaginarAsync(PaginaConsulta.Desde(pagina, tamano), ct);

        return Ok(new { pedidos = items, pagina = paginaResponse });
    }

    // Resumen general de conteos por estado (para métricas y badges).
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
    {
        var conteosPorEstado = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.Estado)
            .Select(g => new { estado = g.Key.ToString(), cantidad = g.Count() })
            .ToListAsync(ct);

        var total = await db.Pedidos.CountAsync(p => p.Activo, ct);

        return Ok(new { conteos_por_estado = conteosPorEstado, total });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken ct)
    {
        var pedido = await (
            from p in db.Pedidos.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on p.ClienteId equals c.Id
            where p.Id == id && p.Activo
            select new { p, c }
        ).FirstOrDefaultAsync(ct);

        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El remito/orden de carga no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido.p, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para consultar esta orden de carga." });

        var detalles = await (
            from d in db.DetallesPedido.AsNoTracking()
            join prod in db.Productos.AsNoTracking() on d.ProductoId equals prod.Id
            where d.PedidoId == id
            orderby prod.Nombre
            select new DetallePedidoResponse(
                d.Id,
                prod.Id,
                prod.Nombre,
                d.Cantidad,
                d.PrecioUnitario,
                d.Cantidad * d.PrecioUnitario
            )
        ).ToListAsync(ct);

        var total = Math.Round(detalles.Sum(d => d.Subtotal), 2);

        var resultado = new PedidoDetalleResponse(
            pedido.p.Id,
            pedido.p.Numero,
            pedido.p.Estado.ToString(),
            pedido.p.FechaPedido,
            pedido.p.Observaciones,
            new ClientePedidoResponse(pedido.c.Id, pedido.c.RazonSocial, pedido.c.Cuit),
            detalles,
            total,
            pedido.p.LatitudEntrega,
            pedido.p.LongitudEntrega,
            pedido.p.FechaEnPuerta,
            pedido.p.ChoferId,
            pedido.p.PickingResultado,
            ItemsEditables: pedido.p.PickingResultado is null
                && EstadosConItemsEditables.Contains(pedido.p.Estado),
            ObservacionesEditables: pedido.p.Estado != EstadoPedido.Entregado
                && pedido.p.Estado != EstadoPedido.Cancelado
        );

        return Ok(new { pedido = resultado });
    }


    // Creación de orden de carga: LOS PRECIOS NO VIAJAN DESDE EL TELÉFONO.
    // El servidor lee los precios vigentes de la base de datos para preservar la integridad contable.
    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearPedidoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        // 1. Verificar idempotencia
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "CREAR_PEDIDO", ct);
            if (op?.ResultadoJson != null)
            {
                return Content(op.ResultadoJson, "application/json");
            }
        }

        // Un CLIENTE solo puede crear pedidos a su propio nombre: el cliente_id del
        // body se ignora y se resuelve contra la sesión. Admin y Operario sí eligen.
        long clienteId;
        if (User.EsCliente())
        {
            var propio = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (propio is null)
                return StatusCode(403, new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial. Contactá al administrador." });

            clienteId = propio.Id;
        }
        else
        {
            if (request.ClienteId <= 0)
                return BadRequest(new { codigo = "cliente_requerido", mensaje = "Debe especificar un cliente comercial válido." });

            clienteId = request.ClienteId;
        }

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId && c.Activo, ct);
        if (cliente is null)
            return BadRequest(new { codigo = "cliente_no_encontrado", mensaje = "El cliente seleccionado no existe o está inactivo." });

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { codigo = "items_requeridos", mensaje = "La orden de carga debe contener al menos un producto." });

        var productosIds = request.Items.Select(i => i.ProductoId).Distinct().ToList();
        var productosDb = await db.Productos
            .Where(p => productosIds.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id, ct);

        // Validar existencias
        var tieneFalloValidacion = false;
        var detalleValidacion = new List<string>();

        foreach (var item in request.Items)
        {
            if (!productosDb.TryGetValue(item.ProductoId, out var prod))
                return BadRequest(new { codigo = "producto_invalido", mensaje = $"El producto con ID {item.ProductoId} no existe o está inactivo." });

            if (item.Cantidad <= 0)
                return BadRequest(new { codigo = "cantidad_invalida", mensaje = $"La cantidad para '{prod.Nombre}' debe ser mayor a cero." });

            if (!prod.Disponible)
            {
                tieneFalloValidacion = true;
                detalleValidacion.Add($"Producto '{prod.Nombre}' no disponible en catálogo");
            }

            if (prod.StockActual < item.Cantidad)
            {
                return BadRequest(new { codigo = "stock_insuficiente", mensaje = $"Stock insuficiente para '{prod.Nombre}'. Stock disponible: {prod.StockActual}, solicitado: {item.Cantidad}." });
            }
        }

        // Generación de número de remito transaccional sin carreras de concurrencia (P0-5)
        var numeroTemporal = $"TMP-{Guid.NewGuid():N}";

        var estadoInicial = EstadoPedido.Pendiente;
        if (!tieneFalloValidacion && !string.IsNullOrWhiteSpace(request.Estado) &&
            Enum.TryParse<EstadoPedido>(request.Estado, true, out var est) && est == EstadoPedido.Confirmado &&
            User.IsInRole(RolCodigos.Admin))
        {
            estadoInicial = EstadoPedido.Confirmado;
        }

        var nuevoPedido = new Pedido
        {
            ClienteId = clienteId,
            Numero = numeroTemporal,
            Estado = estadoInicial,
            FechaPedido = DateTime.UtcNow,
            Observaciones = NormalizarTexto(request.Observaciones),
            ValidacionFallida = tieneFalloValidacion,
            ValidacionDetalle = tieneFalloValidacion ? string.Join(" · ", detalleValidacion) : null,
            ConfirmadoPorId = estadoInicial == EstadoPedido.Confirmado ? User.UsuarioId() : null,
            ConfirmadoEn = estadoInicial == EstadoPedido.Confirmado ? DateTime.UtcNow : null,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        var esRelacional = db.Database.IsRelational();
        await using var tx = esRelacional ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            db.Pedidos.Add(nuevoPedido);
        await db.SaveChangesAsync(ct);

        // Correlativo formal garantizado por secuencia autonumérica sin colisión
        nuevoPedido.Numero = $"PED-{nuevoPedido.Id:D6}";

        foreach (var item in request.Items)
        {
            var prod = productosDb[item.ProductoId];
            prod.StockActual -= item.Cantidad; // Descuento atómico de stock (D2)
            db.DetallesPedido.Add(new DetallePedido
            {
                PedidoId = nuevoPedido.Id,
                ProductoId = prod.Id,
                Cantidad = item.Cantidad,
                CantidadPedida = item.Cantidad,
                CantidadPreparada = item.Cantidad,
                PrecioUnitario = prod.Precio, // Precio histórico congelado al crear
                CreadoEn = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        if (tx != null)
        {
            await tx.CommitAsync(ct);
        }
    }
    catch
    {
        if (tx != null)
        {
            await tx.RollbackAsync(ct);
        }
        throw;
    }

        var respuestaObj = new
        {
            pedido_id = nuevoPedido.Id,
            numero = nuevoPedido.Numero,
            estado = nuevoPedido.Estado.ToString(),
            validacion_fallida = nuevoPedido.ValidacionFallida,
            validacion_detalle = nuevoPedido.ValidacionDetalle
        };

        var respuestaJson = System.Text.Json.JsonSerializer.Serialize(respuestaObj);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "CREAR_PEDIDO",
                User.UsuarioId(),
                nuevoPedido.Id,
                respuestaJson,
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Transición de estado evaluada con la máquina centralizada IPedidoTransicionService
    [HttpPut("{id:long}/estado")]
    public async Task<IActionResult> CambiarEstado(
        long id,
        [FromBody] CambiarEstadoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "CAMBIAR_ESTADO", ct);
            if (op?.ResultadoJson != null)
            {
                return Content(op.ResultadoJson, "application/json");
            }
        }

        if (!Enum.TryParse<EstadoPedido>(request.Estado, true, out var nuevoEstado))
            return BadRequest(new { codigo = "estado_invalido", mensaje = $"El estado '{request.Estado}' no es un estado logístico reconocido." });

        if (!MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FotoUrl) ||
            !MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FirmaUrl))
        {
            return BadRequest(new { codigo = "evidencia_invalida", mensaje = "La evidencia de entrega (foto o firma) tiene un formato inválido o no permitido." });
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue dado de baja." });

        if (!string.IsNullOrWhiteSpace(request.FotoUrl)) pedido.EntregaFotoUrl = request.FotoUrl;
        if (!string.IsNullOrWhiteSpace(request.FirmaUrl)) pedido.EntregaFirmaUrl = request.FirmaUrl;

        // No se despacha sin chofer: si no, el pedido queda fuera de la ruta de todos.
        if (nuevoEstado == EstadoPedido.Despachado
            && pedido.Estado != EstadoPedido.Despachado
            && !pedido.ChoferId.HasValue)
        {
            return BadRequest(new { codigo = "chofer_requerido", mensaje = "Asigná un chofer antes de despachar el pedido." });
        }

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty,
            Latitud: request.Latitud,
            Longitud: request.Longitud,
            EsSincronizacionOffline: request.EsOffline,
            OcurridoEn: request.OcurridoEn
        );

        var estadoAnterior = pedido.Estado;
        var resultado = await transicionService.TransicionarAsync(pedido, nuevoEstado, contexto, ct);
        if (!resultado.Exito)
        {
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });
        }

        await AvisarConflictosOfflineAsync(pedido, estadoAnterior, request.EsOffline, nuevoEstado, ct);

        await db.SaveChangesAsync(ct);

        var respuestaObj = new
        {
            estado = pedido.Estado.ToString(),
            latitud = pedido.LatitudEntrega,
            longitud = pedido.LongitudEntrega
        };

        var respuestaJson = System.Text.Json.JsonSerializer.Serialize(respuestaObj);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "CAMBIAR_ESTADO",
                User.UsuarioId(),
                pedido.Id,
                respuestaJson,
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Cancelación lógica de una orden de carga
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o ya fue cancelado." });

        // Si es cliente, no puede cancelar directo: debe usar /solicitar-cancelacion (Decisión 12)
        if (User.EsCliente())
        {
            return BadRequest(new
            {
                codigo = "requiere_solicitud",
                mensaje = "Los clientes deben solicitar la cancelación para aprobación del Administrador."
            });
        }

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty
        );

        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Cancelado, contexto, ct);
        if (!resultado.Exito)
        {
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { cancelado = true, estado = pedido.Estado.ToString() });
    }

    // Edición de pedido exclusivo para ADMINISTRADOR
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> ActualizarPedido(
        long id,
        [FromBody] EditarPedidoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "EDITAR_PEDIDO", ct);
            if (op?.ResultadoJson != null)
            {
                return Content(op.ResultadoJson, "application/json");
            }
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });


        if (pedido.Estado == EstadoPedido.Entregado || pedido.Estado == EstadoPedido.Cancelado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede editar un pedido finalizado o cancelado." });

        // Los ítems solo se tocan antes del picking y fuera del reparto: cambiar la
        // carga de un pedido que ya está en la calle rompe la operación del chofer.
        var cambiaItems = request.Items is not null && request.Items.Count > 0;

        if (cambiaItems)
        {
            if (pedido.PickingResultado is not null || !EstadosConItemsEditables.Contains(pedido.Estado))
            {
                return BadRequest(new
                {
                    codigo = "items_bloqueados",
                    mensaje = "El pedido ya inició preparación o está en reparto: solo podés editar las observaciones."
                });
            }

            var detallesActuales = await db.DetallesPedido.Where(d => d.PedidoId == id).ToListAsync(ct);

            var productosIds = request.Items!.Select(i => i.ProductoId).Distinct().ToList();
            var productosDb = await db.Productos
                .Where(p => productosIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var resultado = PedidoEdicionService.Reconciliar(
                pedido.Id,
                detallesActuales,
                request.Items!.Select(i => new ItemEdicionPedido(i.ProductoId, i.Cantidad, i.PrecioUnitario)).ToList(),
                productosDb);

            if (resultado.Error is not null)
            {
                return BadRequest(new
                {
                    codigo = resultado.Error.Codigo,
                    mensaje = resultado.Error.Mensaje
                });
            }

            // La edición no puede dejar la orden por debajo de lo que ya se cobró.
            var yaCobrado = await db.CobrosPedido
                .Where(c => c.PedidoId == pedido.Id)
                .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

            const decimal tolerancia = 0.01m;
            if (yaCobrado > 0m && resultado.TotalResultante + tolerancia < yaCobrado)
            {
                return BadRequest(new
                {
                    codigo = "total_menor_a_cobrado",
                    mensaje = $"La orden no puede quedar en ${resultado.TotalResultante:N2}: ya se cobraron ${yaCobrado:N2}."
                });
            }

            db.DetallesPedido.RemoveRange(resultado.Eliminados);
            db.DetallesPedido.AddRange(resultado.Agregados);
        }

        if (request.Observaciones is not null)
        {
            pedido.Observaciones = NormalizarTexto(request.Observaciones);
        }

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var respuesta = new { mensaje = "Pedido actualizado correctamente.", id = pedido.Id };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "EDITAR_PEDIDO",
                User.UsuarioId(),
                pedido.Id,
                System.Text.Json.JsonSerializer.Serialize(respuesta),
                ct
            );
        }

        return Ok(respuesta);
    }

    // Confirmación explícita de pedido para ADMINISTRADOR (CU-19 / Decisión 1)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPost("{id:long}/confirmar")]
    public async Task<IActionResult> ConfirmarPedido(
        long id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "CONFIRMAR_PEDIDO", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty
        );

        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Confirmado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await db.SaveChangesAsync(ct);

        var respuestaObj = new
        {
            mensaje = $"Pedido {pedido.Numero} confirmado exitosamente.",
            estado = pedido.Estado.ToString(),
            confirmado_por_id = pedido.ConfirmadoPorId,
            confirmado_en = pedido.ConfirmadoEn
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "CONFIRMAR_PEDIDO",
                User.UsuarioId(),
                pedido.Id,
                System.Text.Json.JsonSerializer.Serialize(respuestaObj),
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Solicitud de cancelación por parte del CLIENTE (CU-09 / Decisión 12)
    [Authorize(Roles = RolCodigos.Cliente)]
    [HttpPost("{id:long}/solicitar-cancelacion")]
    public async Task<IActionResult> SolicitarCancelacion(long id, [FromBody] SolicitarCancelacionRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado || pedido.Estado == EstadoPedido.Cancelado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede solicitar la cancelación de un pedido finalizado o cancelado." });

        pedido.CancelacionEstado = "Solicitada";
        pedido.CancelacionMotivo = string.IsNullOrWhiteSpace(request?.Motivo) ? "Solicitado por el cliente" : request.Motivo.Trim();
        pedido.CancelacionSolicitadaPorId = User.UsuarioId();
        pedido.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = "Solicitud de cancelación enviada para evaluación del Administrador.",
            cancelacion_estado = pedido.CancelacionEstado,
            motivo = pedido.CancelacionMotivo
        });
    }

    // Resolución de cancelación por el ADMINISTRADOR (CU-09 / Decisión 12)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPost("{id:long}/resolver-cancelacion")]
    public async Task<IActionResult> ResolverCancelacion(long id, [FromBody] ResolverCancelacionRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (request.Aprobada)
        {
            var contexto = new MercuryGo.Api.Services.ContextoTransicion(
                UsuarioId: User.UsuarioId(),
                RolCodigo: User.RolCodigo() ?? string.Empty
            );

            var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Cancelado, contexto, ct);
            if (!resultado.Exito)
                return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

            pedido.CancelacionEstado = "Aprobada";
        }
        else
        {
            pedido.CancelacionEstado = "Rechazada";
        }

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = request.Aprobada ? "Cancelación aprobada y stock restituido." : "Cancelación rechazada.",
            estado = pedido.Estado.ToString(),
            cancelacion_estado = pedido.CancelacionEstado
        });
    }

    // Avisos al Admin cuando una acción tomada sin red choca con lo que pasó
    // entretanto en el servidor (Decisiones 10 y 17). La acción se aplica igual
    // —gana el chofer— pero el Admin tiene que enterarse.
    private async Task AvisarConflictosOfflineAsync(
        Pedido pedido,
        EstadoPedido estadoAnterior,
        bool esOffline,
        EstadoPedido estadoNuevo,
        CancellationToken ct)
    {
        if (!esOffline)
            return;

        var usuarioId = User.UsuarioId();
        var esChofer = string.Equals(User.RolCodigo(), RolCodigos.Chofer, StringComparison.OrdinalIgnoreCase);

        var avisosCancelacion = await conflictoOfflineService.AvisarEntregaSobreCanceladoAsync(
            pedido, usuarioId, estadoAnterior, ct);

        if (avisosCancelacion > 0)
            return;

        await conflictoOfflineService.AvisarChoferReasignadoAsync(
            pedido, usuarioId, esChofer, estadoNuevo, ct);
    }

}