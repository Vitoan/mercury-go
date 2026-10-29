using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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
    IWebHostEnvironment env,
    MercuryGo.Api.Services.IPedidoTransicionService transicionService,
    MercuryGo.Api.Services.IdempotenciaService idempotenciaService,
    MercuryGo.Api.Services.NotificacionService notificacionService,
    MercuryGo.Api.Services.ConflictoOfflineService conflictoOfflineService) : ControllerBase
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

    private async Task<Cliente?> ObtenerClienteDeUsuarioAutenticadoAsync(CancellationToken ct)
    {
        var usuarioId = User.UsuarioId();
        if (usuarioId > 0)
        {
            var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);
            if (usuario?.ClienteId != null)
            {
                var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == usuario.ClienteId.Value && c.Activo, ct);
                if (cliente != null) return cliente;
            }
        }

        var emailUsuario = User.Email();
        if (!string.IsNullOrWhiteSpace(emailUsuario))
        {
            return await db.Clientes.FirstOrDefaultAsync(c => c.Email == emailUsuario && c.Activo, ct);
        }

        return null;
    }

    private async Task<bool> PuedeAccederAlPedidoAsync(Pedido pedido, CancellationToken ct)
    {
        if (User.IsInRole(RolCodigos.Admin) || User.IsInRole(RolCodigos.Operario))
            return true;

        if (User.IsInRole(RolCodigos.Chofer))
        {
            var usuarioId = User.UsuarioId();
            return (pedido.ChoferId.HasValue && pedido.ChoferId.Value == usuarioId)
                || pedido.Estado == EstadoPedido.Despachado;
        }

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            return cliente != null && pedido.ClienteId == cliente.Id;
        }

        return false;
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
                request.Items!.Select(i => new ItemEdicionPedido(i.ProductoId, i.Cantidad)).ToList(),
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

    // Registro de cobro en destino por parte del CHOFER (Efectivo o Transferencia / CU-13 / Decisión 9)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/cobro-chofer")]
    public async Task<IActionResult> RegistrarCobroChofer(
        long id,
        [FromBody] CobroChoferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "COBRO_CHOFER", ct);
            if (op?.ResultadoJson != null)
            {
                return Content(op.ResultadoJson, "application/json");
            }
        }

        if (request.Monto <= 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto cobrado debe ser mayor a cero." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        // Solo se cobra una orden en reparto. El Admin puede registrar el cobro
        // después de la entrega como ajuste; el Chofer, no.
        var esAdmin = User.IsInRole(RolCodigos.Admin);
        var estadosCobrables = esAdmin
            ? new[] { EstadoPedido.Despachado, EstadoPedido.EnPuerta, EstadoPedido.Entregado }
            : new[] { EstadoPedido.Despachado, EstadoPedido.EnPuerta };

        if (!estadosCobrables.Contains(pedido.Estado))
            return BadRequest(new { codigo = "estado_invalido", mensaje = $"No se puede registrar un cobro sobre un pedido en estado '{pedido.Estado}'." });

        // Un chofer solo cobra los pedidos que tiene asignados.
        if (!esAdmin && pedido.ChoferId.HasValue && pedido.ChoferId.Value != User.UsuarioId())
            return StatusCode(403, new { codigo = "chofer_no_asignado", mensaje = "El pedido está asignado a otro chofer." });

        // El acumulado de cobros no puede superar el total de la orden.
        var totalPedido = await db.DetallesPedido
            .Where(d => d.PedidoId == pedido.Id)
            .SumAsync(d => (decimal?)(d.Cantidad * d.PrecioUnitario), ct) ?? 0m;
        var yaCobrado = await db.CobrosPedido
            .Where(c => c.PedidoId == pedido.Id)
            .SumAsync(c => (decimal?)c.Monto, ct) ?? 0m;

        const decimal tolerancia = 0.01m;
        if (yaCobrado + request.Monto > totalPedido + tolerancia)
        {
            return BadRequest(new
            {
                codigo = "monto_excede_total",
                mensaje = $"El cobro supera el saldo de la orden. Total: ${totalPedido:N2}, ya cobrado: ${yaCobrado:N2}."
            });
        }

        var metodo = string.IsNullOrWhiteSpace(request.Metodo) ? "Efectivo" : request.Metodo.Trim();
        var ocurridoEn = request.OcurridoEn ?? DateTime.UtcNow;

        // 1. Guardar en tabla relacional estructurada
        var nuevoCobro = new CobroPedido
        {
            PedidoId = pedido.Id,
            Monto = request.Monto,
            Metodo = metodo,
            RegistradoPorId = User.UsuarioId(),
            OcurridoEn = ocurridoEn,
            RecibidoEn = DateTime.UtcNow
        };
        db.CobrosPedido.Add(nuevoCobro);

        // 2. Preservar etiqueta en Observaciones para total retrocompatibilidad
        var logCobro = $"[Cobro Chofer: ${request.Monto:N2} ({metodo})]";
        var obs = pedido.Observaciones ?? string.Empty;
        pedido.Observaciones = string.IsNullOrWhiteSpace(obs) ? logCobro : $"{obs} · {logCobro}";
        pedido.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var respuestaObj = new
        {
            mensaje = $"Cobro de ${request.Monto:N2} registrado exitosamente.",
            cobro_id = nuevoCobro.Id,
            monto = nuevoCobro.Monto,
            metodo = nuevoCobro.Metodo,
            observaciones = pedido.Observaciones
        };

        var respuestaJson = System.Text.Json.JsonSerializer.Serialize(respuestaObj);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "COBRO_CHOFER",
                User.UsuarioId(),
                pedido.Id,
                respuestaJson,
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Resumen de Cuenta Corriente y Deuda para el CLIENTE autenticado
    [HttpGet("cuenta-corriente")]
    public async Task<IActionResult> ObtenerCuentaCorriente(CancellationToken ct)
    {
        var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });

        var pedidosCliente = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.ClienteId == cliente.Id && p.Activo && p.Estado != EstadoPedido.Cancelado)
            .Select(p => new
            {
                p.Id,
                p.Numero,
                p.Estado,
                p.FechaPedido,
                Total = db.DetallesPedido.Where(d => d.PedidoId == p.Id).Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                p.Observaciones
            })
            .ToListAsync(ct);

        var pagosCuenta = await db.PagosCuentaCorriente
            .AsNoTracking()
            .Where(p => p.ClienteId == cliente.Id && p.Activo)
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync(ct);

        var totalFacturado = pedidosCliente.Sum(p => p.Total);
        var totalPagado = pagosCuenta.Where(p => p.Estado == "Aprobado").Sum(p => p.Monto);
        var comprobantesPendientes = pagosCuenta.Count(p => p.Estado == "Pendiente");

        // Sumar pagos aprobados y calcular pendientes de auditoría
        foreach (var p in pedidosCliente)
        {
            var obs = p.Observaciones ?? string.Empty;
            if (obs.Contains("[Comprobante de pago: APROBADO"))
            {
                var matchMonto = System.Text.RegularExpressions.Regex.Match(obs, @"\[Comprobante de pago:\s*APROBADO\s*·\s*\$([\d\.,]+)");
                if (matchMonto.Success && decimal.TryParse(matchMonto.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var montoAprob))
                {
                    totalPagado += montoAprob;
                }
                else
                {
                    totalPagado += p.Total;
                }
            }
            else if (obs.Contains("[Cobro Chofer:"))
            {
                var matchCobro = System.Text.RegularExpressions.Regex.Match(obs, @"\[Cobro Chofer:\s*\$([\d\.,]+)");
                if (matchCobro.Success && decimal.TryParse(matchCobro.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var montoCobro))
                {
                    totalPagado += montoCobro;
                }
                else
                {
                    totalPagado += p.Total;
                }
            }
            else if (obs.Contains("[Comprobante de pago: PENDIENTE") || obs.Contains("[Comprobante de pago: /uploads/"))
            {
                comprobantesPendientes++;
            }
        }

        var saldoDeudor = Math.Max(0m, totalFacturado - totalPagado);

        return Ok(new
        {
            cliente_id = cliente.Id,
            razon_social = cliente.RazonSocial,
            cuit = cliente.Cuit,
            total_facturado = totalFacturado,
            total_pagado = totalPagado,
            saldo_deudor = saldoDeudor,
            comprobantes_pendientes = comprobantesPendientes,
            pedidos_pendientes = pedidosCliente.Count(p => p.Estado != EstadoPedido.Entregado),
            pagos_cuenta_corriente = pagosCuenta.Select(p => new
            {
                p.Id,
                p.Monto,
                p.Estado,
                p.ComprobanteUrl,
                p.Observaciones,
                p.MotivoRechazo,
                p.CreadoEn
            })
        });
    }

    // Listado consolidado de saldos comerciales y comprobantes a auditar (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpGet("cuentas-corrientes-clientes")]
    public async Task<IActionResult> ListarCuentasCorrientesClientes(CancellationToken ct)
    {
        var clientes = await db.Clientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .ToListAsync(ct);

        var pedidos = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Activo && p.Estado != EstadoPedido.Cancelado)
            .Select(p => new
            {
                p.Id,
                p.Numero,
                p.ClienteId,
                p.Estado,
                p.FechaPedido,
                Total = db.DetallesPedido.Where(d => d.PedidoId == p.Id).Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                p.Observaciones
            })
            .ToListAsync(ct);

        var todosPagosCuenta = await db.PagosCuentaCorriente
            .AsNoTracking()
            .Where(p => p.Activo)
            .OrderByDescending(p => p.CreadoEn)
            .ToListAsync(ct);

        var resumenPorCliente = clientes.Select(c =>
        {
            var pedidosC = pedidos.Where(p => p.ClienteId == c.Id).ToList();
            var pagosC = todosPagosCuenta.Where(p => p.ClienteId == c.Id).ToList();
            var totalFacturado = pedidosC.Sum(p => p.Total);
            var totalPagado = pagosC.Where(p => p.Estado == "Aprobado").Sum(p => p.Monto);
            var pendientesRevision = pagosC.Count(p => p.Estado == "Pendiente");
            var comprobantesPendientes = new List<object>();

            foreach (var pc in pagosC.Where(p => p.Estado == "Pendiente"))
            {
                comprobantesPendientes.Add(new
                {
                    pago_cuenta_id = pc.Id,
                    cliente_id = pc.ClienteId,
                    monto = pc.Monto,
                    fecha = pc.CreadoEn,
                    comprobante_url = pc.ComprobanteUrl,
                    observaciones = pc.Observaciones,
                    tipo = "cuenta_corriente"
                });
            }

            foreach (var p in pedidosC)
            {
                var obs = p.Observaciones ?? string.Empty;
                if (obs.Contains("[Comprobante de pago: APROBADO"))
                {
                    var matchMonto = System.Text.RegularExpressions.Regex.Match(obs, @"\[Comprobante de pago:\s*APROBADO\s*·\s*\$([\d\.,]+)");
                    if (matchMonto.Success && decimal.TryParse(matchMonto.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var montoAprob))
                    {
                        totalPagado += montoAprob;
                    }
                    else
                    {
                        totalPagado += p.Total;
                    }
                }
                else if (obs.Contains("[Cobro Chofer:"))
                {
                    var matchCobro = System.Text.RegularExpressions.Regex.Match(obs, @"\[Cobro Chofer:\s*\$([\d\.,]+)");
                    if (matchCobro.Success && decimal.TryParse(matchCobro.Groups[1].Value.Replace(".", string.Empty).Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var montoCobro))
                    {
                        totalPagado += montoCobro;
                    }
                    else
                    {
                        totalPagado += p.Total;
                    }
                }
                else if (obs.Contains("[Comprobante de pago: PENDIENTE") || obs.Contains("[Comprobante de pago: /uploads/"))
                {
                    pendientesRevision++;
                    var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
                    var comprobanteUrl = match.Success ? match.Value : null;

                    comprobantesPendientes.Add(new
                    {
                        pedido_id = p.Id,
                        pedido_numero = p.Numero,
                        fecha = p.FechaPedido,
                        total = p.Total,
                        comprobante_url = comprobanteUrl,
                        observaciones = p.Observaciones
                    });
                }
            }

            var saldoDeudor = Math.Max(0m, totalFacturado - totalPagado);

            return new
            {
                cliente_id = c.Id,
                razon_social = c.RazonSocial,
                cuit = c.Cuit,
                telefono = c.Telefono,
                email = c.Email,
                localidad = c.Localidad,
                total_facturado = totalFacturado,
                total_pagado = totalPagado,
                saldo_deudor = saldoDeudor,
                comprobantes_pendientes_conteo = pendientesRevision,
                comprobantes_pendientes = comprobantesPendientes
            };
        }).ToList();

        var deudaTotalGeneral = resumenPorCliente.Sum(x => x.saldo_deudor);
        var clientesConDeuda = resumenPorCliente.Count(x => x.saldo_deudor > 0);
        var totalPendientesRevision = resumenPorCliente.Sum(x => x.comprobantes_pendientes_conteo);

        return Ok(new
        {
            metricas = new
            {
                deuda_total = deudaTotalGeneral,
                clientes_con_deuda = clientesConDeuda,
                total_comprobantes_pendientes = totalPendientesRevision
            },
            clientes = resumenPorCliente
        });
    }

    // Aprobación de comprobante bancario por el ADMIN (acredita el pago y reduce la deuda con el monto verificado / CU-15)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("{id:long}/aprobar-comprobante")]
    public async Task<IActionResult> AprobarComprobante(long id, [FromBody] AprobarComprobanteRequest? request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var totalPedido = await db.DetallesPedido
            .Where(d => d.PedidoId == id)
            .SumAsync(d => (decimal?)d.Cantidad * d.PrecioUnitario, ct) ?? 0m;

        var montoAprobado = request?.Monto.HasValue == true && request.Monto.Value > 0
            ? request.Monto.Value
            : totalPedido;

        var obs = pedido.Observaciones ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
        var ruta = match.Success ? match.Value : (pedido.ComprobanteUrl ?? string.Empty);

        // Actualizar campos estructurados (Fase 1 / Sección 4)
        pedido.ComprobanteEstado = "Aprobado";
        pedido.ComprobanteUrl = string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        pedido.AuditadoPorId = User.UsuarioId();
        pedido.AuditadoEn = DateTime.UtcNow;

        // Limpiar cualquier etiqueta previa de comprobante
        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obs, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();
        var nuevaEtiqueta = $"[Comprobante de pago: APROBADO · ${montoAprobado:N2} · {DateTime.UtcNow:dd/MM/yyyy HH:mm} · {ruta}]";

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? nuevaEtiqueta
            : $"{obsLimpia} · {nuevaEtiqueta}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Comprobante de la orden {pedido.Numero} aprobado por ${montoAprobado:N2}. Pago acreditado exitosamente.",
            pedido_id = pedido.Id,
            comprobante_estado = pedido.ComprobanteEstado,
            monto_acreditado = montoAprobado,
            observaciones = pedido.Observaciones
        });
    }

    // Rechazo de comprobante bancario por el ADMIN (no descuenta y almacena motivo / CU-15)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("{id:long}/rechazar-comprobante")]
    public async Task<IActionResult> RechazarComprobante(long id, [FromBody] RechazarComprobanteRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var obs = pedido.Observaciones ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(obs, @"/uploads/comprobantes/[^\s\]]+");
        var ruta = match.Success ? match.Value : (pedido.ComprobanteUrl ?? string.Empty);

        var motivo = string.IsNullOrWhiteSpace(request?.Motivo) ? "No acreditado en cuenta bancaria" : request.Motivo.Trim();

        // Actualizar campos estructurados
        pedido.ComprobanteEstado = "Rechazado";
        pedido.ComprobanteUrl = string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        pedido.AuditadoPorId = User.UsuarioId();
        pedido.AuditadoEn = DateTime.UtcNow;

        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obs, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();
        var nuevaEtiqueta = $"[Comprobante de pago: RECHAZADO - Motivo: {motivo} · {ruta}]";

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? nuevaEtiqueta
            : $"{obsLimpia} · {nuevaEtiqueta}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Comprobante de la orden {pedido.Numero} rechazado: {motivo}",
            pedido_id = pedido.Id,
            comprobante_estado = pedido.ComprobanteEstado,
            observaciones = pedido.Observaciones
        });
    }

    // Registro de picking/preparación de depósito (Operario o Admin)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpPut("{id:long}/picking")]
    public async Task<IActionResult> ActualizarPicking(long id, [FromBody] ActualizarPickingRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { codigo = "items_requeridos", mensaje = "Debe enviar el estado de picking de los productos." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede realizar picking sobre un pedido finalizado." });

        // Decisión 16: si el pedido se canceló mientras el operario preparaba sin red,
        // el picking se guarda igual (es trabajo físico real), pero la orden sigue
        // Cancelada y no se tocan sus importes.
        var pedidoCancelado = pedido.Estado == EstadoPedido.Cancelado;

        var detalles = await (
            from d in db.DetallesPedido
            join prod in db.Productos on d.ProductoId equals prod.Id
            where d.PedidoId == id
            select new { d, ProductoNombre = prod.Nombre }
        ).ToListAsync(ct);

        var mapaRequest = request.Items.ToDictionary(i => i.ProductoId);
        var faltantes = new List<string>();
        var esCompleto = true;

        foreach (var item in detalles)
        {
            if (mapaRequest.TryGetValue(item.d.ProductoId, out var pick))
            {
                item.d.CantidadPreparada = Math.Max(0, pick.CantidadPreparada);
                var enFalta = string.Equals(pick.EstadoItem, "EnFalta", StringComparison.OrdinalIgnoreCase) || pick.CantidadPreparada < item.d.Cantidad;
                if (enFalta)
                {
                    esCompleto = false;
                    faltantes.Add($"{item.ProductoNombre} (prep. {pick.CantidadPreparada}/{item.d.Cantidad})");

                    // Decisión 5: si el picking queda incompleto, se ajusta la cantidad
                    // definitiva de la orden. Sobre una orden cancelada no se toca el
                    // importe: solo se registra lo que efectivamente se preparó.
                    var diferencia = item.d.Cantidad - item.d.CantidadPreparada;
                    if (!pedidoCancelado && diferencia > 0)
                    {
                        item.d.Cantidad = item.d.CantidadPreparada;
                    }
                }
            }
            else
            {
                esCompleto = false;
                faltantes.Add($"{item.ProductoNombre} (no verificado)");
            }
        }

        pedido.PickingResultado = esCompleto ? "Completo" : "Incompleto";
        pedido.PickingEn = DateTime.UtcNow;

        var logPicking = esCompleto
            ? "[Picking: COMPLETO]"
            : $"[Picking: INCOMPLETO - Faltantes: {string.Join(", ", faltantes)}]";

        var obsActual = pedido.Observaciones ?? string.Empty;
        var patronPicking = System.Text.RegularExpressions.Regex.Replace(obsActual, @"\s*·?\s*\[Picking:[^\]]+\]", string.Empty).Trim();

        pedido.Observaciones = string.IsNullOrWhiteSpace(patronPicking)
            ? logPicking
            : $"{patronPicking} · {logPicking}";

        if (pedido.Estado == EstadoPedido.Confirmado)
        {
            pedido.Estado = EstadoPedido.EnPreparacion;
        }

        pedido.ActualizadoEn = DateTime.UtcNow;

        // Aviso al Admin cuando falta mercadería: sin esto, el faltante solo se ve
        // si alguien abre la orden.
        if (request.EsOffline && pedidoCancelado)
        {
            // Decisión 16: el picking se guardó, pero sobre una orden que el Admin
            // ya había cancelado. Un solo aviso, que incluye los faltantes.
            await conflictoOfflineService.AvisarPickingSobreCanceladoAsync(
                pedido, User.UsuarioId(), faltantes.Count > 0, request.EsOffline, ct);
        }
        else if (faltantes.Count > 0)
        {
            var detalleFaltantes = string.Join("; ", faltantes);
            if (detalleFaltantes.Length > 400)
                detalleFaltantes = detalleFaltantes[..400] + "…";

            await notificacionService.NotificarRolAsync(
                RolCodigos.Admin,
                $"⚠️ Picking incompleto ({pedido.Numero})",
                $"Faltantes: {detalleFaltantes}",
                "PEDIDO",
                pedido.Id,
                ct);
        }

        // Terminar el picking deja el pedido listo para salir: si ya tiene chofer,
        // se despacha en la misma acción (CU-11). Sin chofer queda en preparación.
        var despachado = false;
        var requiereChofer = false;

        if (pedido.Estado == EstadoPedido.EnPreparacion)
        {
            if (pedido.ChoferId.HasValue)
            {
                var contexto = new ContextoTransicion(
                    UsuarioId: User.UsuarioId(),
                    RolCodigo: User.RolCodigo() ?? string.Empty,
                    OcurridoEn: DateTime.UtcNow);

                var transicion = await transicionService.TransicionarAsync(
                    pedido, EstadoPedido.Despachado, contexto, ct);

                if (!transicion.Exito)
                    return BadRequest(new { codigo = transicion.CodigoError, mensaje = transicion.MensajeError });

                despachado = true;
            }
            else
            {
                requiereChofer = true;
            }
        }

        await db.SaveChangesAsync(ct);

        var mensaje = pedidoCancelado
            ? "Picking registrado sobre un pedido cancelado. La orden sigue cancelada."
            : despachado
                ? esCompleto
                    ? "Picking completo. Pedido despachado y asignado al chofer."
                    : "Picking con faltantes. Pedido ajustado y despachado con lo preparado."
                : requiereChofer
                    ? "Picking guardado. Asigná un chofer para despachar el pedido."
                    : esCompleto
                        ? "Picking completado exitosamente. Todo listo para despacho."
                        : "Picking registrado con faltantes. Pedido ajustado a lo preparado.";

        return Ok(new
        {
            completo = esCompleto,
            estado = pedido.Estado.ToString(),
            picking_resultado = pedido.PickingResultado,
            faltantes,
            observaciones = pedido.Observaciones,
            despachado,
            requiere_chofer = requiereChofer,
            mensaje
        });
    }


    // Subir pago directo a Cuenta Corriente con comprobante (D1)
    [HttpPost("cuenta-corriente/pagos")]
    public async Task<IActionResult> RegistrarPagoCuentaCorriente(
        [FromForm] decimal monto,
        [FromForm] string? observaciones,
        IFormFile? archivo,
        CancellationToken ct)
    {
        if (monto <= 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto a abonar debe ser mayor a cero." });

        Cliente? cliente;
        if (User.EsCliente())
        {
            cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null)
                return StatusCode(403, new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });
        }
        else
        {
            var clienteIdForm = Request.Form["cliente_id"].ToString();
            if (!long.TryParse(clienteIdForm, out var cid) || cid <= 0)
                return BadRequest(new { codigo = "cliente_requerido", mensaje = "Debe especificar el ID del cliente destino." });

            cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == cid && c.Activo, ct);
            if (cliente is null)
                return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El cliente no existe o está inactivo." });
        }

        string? comprobanteUrl = null;
        if (archivo is not null && archivo.Length > 0)
        {
            var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (archivo.Length > 5_000_000 || !extensionesValidas.Contains(extension))
                return BadRequest(new { codigo = "archivo_invalido", mensaje = "El comprobante debe ser un archivo PDF o imagen (JPG, PNG, WEBP) menor o igual a 5 MB." });

            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var carpeta = Path.Combine(webRoot, "uploads", "comprobantes");
            Directory.CreateDirectory(carpeta);

            var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
            var rutaFisica = Path.Combine(carpeta, nombreArchivo);
            await using var stream = System.IO.File.Create(rutaFisica);
            await archivo.CopyToAsync(stream, ct);
            comprobanteUrl = $"/uploads/comprobantes/{nombreArchivo}";
        }

        var pago = new PagoCuentaCorriente
        {
            ClienteId = cliente.Id,
            Monto = monto,
            Estado = "Pendiente",
            ComprobanteUrl = comprobanteUrl,
            Observaciones = NormalizarTexto(observaciones),
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.PagosCuentaCorriente.Add(pago);
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            monto = pago.Monto,
            estado = pago.Estado,
            comprobante_url = pago.ComprobanteUrl,
            mensaje = "Comprobante de pago a cuenta corriente registrado exitosamente. En proceso de verificación."
        });
    }

    // Listar pagos de cuenta corriente
    [HttpGet("cuenta-corriente/pagos")]
    public async Task<IActionResult> ListarPagosCuentaCorriente(CancellationToken ct)
    {
        IQueryable<PagoCuentaCorriente> query = db.PagosCuentaCorriente.AsNoTracking().Where(p => p.Activo);

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null)
                return StatusCode(403, new { codigo = "cliente_no_vinculado", mensaje = "Tu usuario no está vinculado a un cliente comercial." });

            query = query.Where(p => p.ClienteId == cliente.Id);
        }

        var pagos = await query
            .OrderByDescending(p => p.CreadoEn)
            .Select(p => new
            {
                p.Id,
                p.ClienteId,
                p.Monto,
                p.Estado,
                p.ComprobanteUrl,
                p.Observaciones,
                p.MotivoRechazo,
                p.AuditadoPorId,
                p.AuditadoEn,
                p.CreadoEn
            })
            .ToListAsync(ct);

        return Ok(new { pagos });
    }

    // Aprobar pago a cuenta corriente (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cuenta-corriente/pagos/{id:long}/aprobar")]
    public async Task<IActionResult> AprobarPagoCuentaCorriente(long id, [FromBody] AprobarComprobanteRequest? request, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        if (request?.Monto.HasValue == true && request.Monto.Value > 0)
        {
            pago.Monto = request.Monto.Value;
        }

        pago.Estado = "Aprobado";
        pago.AuditadoPorId = User.UsuarioId();
        pago.AuditadoEn = DateTime.UtcNow;
        pago.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Pago a cuenta corriente #{pago.Id} aprobado exitosamente por ${pago.Monto:N2}.",
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            monto = pago.Monto,
            estado = pago.Estado
        });
    }

    // Rechazar pago a cuenta corriente (Exclusivo ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cuenta-corriente/pagos/{id:long}/rechazar")]
    public async Task<IActionResult> RechazarPagoCuentaCorriente(long id, [FromBody] RechazarComprobanteRequest request, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        var motivo = string.IsNullOrWhiteSpace(request?.Motivo) ? "Comprobante no acreditado en cuenta bancaria" : request.Motivo.Trim();

        pago.Estado = "Rechazado";
        pago.MotivoRechazo = motivo;
        pago.AuditadoPorId = User.UsuarioId();
        pago.AuditadoEn = DateTime.UtcNow;
        pago.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Pago a cuenta corriente #{pago.Id} rechazado: {motivo}",
            pago_id = pago.Id,
            cliente_id = pago.ClienteId,
            estado = pago.Estado,
            motivo_rechazo = pago.MotivoRechazo
        });
    }


    // Descarga segura y autenticada de comprobante de pago de un pedido (P0-3)
    [HttpGet("{id:long}/comprobante-pago/archivo")]
    public async Task<IActionResult> ObtenerArchivoComprobantePago(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El remito/orden no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para consultar los comprobantes de esta orden." });

        var rutaRelativa = pedido.ComprobanteUrl;
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            var match = System.Text.RegularExpressions.Regex.Match(pedido.Observaciones ?? string.Empty, @"/uploads/comprobantes/[^\s\]]+");
            if (match.Success)
            {
                rutaRelativa = match.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(rutaRelativa))
            return NotFound(new { codigo = "sin_comprobante", mensaje = "Esta orden no tiene ningún comprobante de pago adjunto." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var rutaLimpia = rutaRelativa.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var rutaFisica = Path.Combine(webRoot, rutaLimpia);

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { codigo = "archivo_no_encontrado", mensaje = "El archivo físico del comprobante no existe en el servidor." });

        var extension = Path.GetExtension(rutaFisica).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(rutaFisica, contentType, Path.GetFileName(rutaFisica));
    }

    // Descarga segura y autenticada de comprobante de pago directo a cuenta corriente (P0-3)
    [HttpGet("cuenta-corriente/pagos/{id:long}/archivo")]
    public async Task<IActionResult> ObtenerArchivoPagoCuentaCorriente(long id, CancellationToken ct)
    {
        var pago = await db.PagosCuentaCorriente.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pago is null)
            return NotFound(new { codigo = "pago_no_encontrado", mensaje = "El pago a cuenta corriente no existe o fue dado de baja." });

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            if (cliente is null || cliente.Id != pago.ClienteId)
                return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para ver este comprobante." });
        }

        if (string.IsNullOrWhiteSpace(pago.ComprobanteUrl))
            return NotFound(new { codigo = "sin_comprobante", mensaje = "Este abono no contiene un comprobante adjunto." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var rutaLimpia = pago.ComprobanteUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var rutaFisica = Path.Combine(webRoot, rutaLimpia);

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { codigo = "archivo_no_encontrado", mensaje = "El archivo físico del comprobante no existe en el servidor." });

        var extension = Path.GetExtension(rutaFisica).ToLowerInvariant();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(rutaFisica, contentType, Path.GetFileName(rutaFisica));
    }

    // Subida de comprobante de transferencia bancaria (PDF o Foto: JPG, PNG, WEBP hasta 5 MB)
    [HttpPost("{id:long}/comprobante-pago")]
    public async Task<IActionResult> SubirComprobantePago(long id, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { codigo = "archivo_requerido", mensaje = "Debe adjuntar una foto o archivo PDF del comprobante de transferencia." });

        var extensionesValidas = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (archivo.Length > 5_000_000 || !extensionesValidas.Contains(extension))
            return BadRequest(new { codigo = "archivo_invalido", mensaje = "El comprobante debe ser un archivo PDF o imagen (JPG, PNG, WEBP) menor o igual a 5 MB." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para adjuntar comprobantes a esta orden." });

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var carpetaComprobantes = Path.Combine(webRoot, "uploads", "comprobantes");
        Directory.CreateDirectory(carpetaComprobantes);

        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
        var rutaFisica = Path.Combine(carpetaComprobantes, nombreArchivo);
        await using var stream = System.IO.File.Create(rutaFisica);
        await archivo.CopyToAsync(stream, ct);

        // Guardamos en campos estructurados (CU-14 / Decisión 9)
        pedido.ComprobanteEstado = "Pendiente";
        pedido.ComprobanteUrl = $"/uploads/comprobantes/{nombreArchivo}";

        // Guardamos también la referencia en observaciones para total retrocompatibilidad
        var etiquetaComprobante = $"[Comprobante de pago: PENDIENTE · /uploads/comprobantes/{nombreArchivo}]";
        var obsActual = pedido.Observaciones ?? string.Empty;
        var obsLimpia = System.Text.RegularExpressions.Regex.Replace(obsActual, @"\s*·?\s*\[Comprobante de pago:[^\]]+\]", string.Empty).Trim();

        pedido.Observaciones = string.IsNullOrWhiteSpace(obsLimpia)
            ? etiquetaComprobante
            : $"{obsLimpia} · {etiquetaComprobante}";

        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            comprobante_url = pedido.ComprobanteUrl,
            comprobante_estado = pedido.ComprobanteEstado,
            mensaje = "Comprobante de pago adjuntado exitosamente a la orden."
        });
    }

    // Comprobante oficial en PDF con QuestPDF y QR impreso al pie
    [HttpGet("{id:long}/comprobante")]
    public async Task<IActionResult> Comprobante(long id, CancellationToken ct)
    {
        var pedido = await (
            from p in db.Pedidos.AsNoTracking()
            join c in db.Clientes.AsNoTracking() on p.ClienteId equals c.Id
            where p.Id == id && p.Activo
            select new { p, c }
        ).FirstOrDefaultAsync(ct);

        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (!await PuedeAccederAlPedidoAsync(pedido.p, ct))
            return StatusCode(StatusCodes.Status403Forbidden, new { codigo = "acceso_denegado", mensaje = "No tenés autorización para descargar el remito de esta orden." });

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

        var pdfBytes = GenerarComprobantePdf(pedido.p, pedido.c, detalles, total);
        return File(pdfBytes, "application/pdf", $"{pedido.p.Numero}.pdf");
    }

    private static byte[] GenerarComprobantePdf(Pedido p, Cliente c, List<DetallePedidoResponse> detalles, decimal total)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(cLeft =>
                        {
                            cLeft.Item().Text("MercuryGO").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                            cLeft.Item().Text("Logística & Distribución B2B").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });
                        r.ConstantItem(140).Column(cRight =>
                        {
                            cRight.Item().AlignRight().Text($"Orden: {p.Numero}").FontSize(12).Bold();
                            cRight.Item().AlignRight().Text($"Fecha: {p.FechaPedido:dd/MM/yyyy HH:mm}").FontSize(9);
                            cRight.Item().AlignRight().Text($"Estado: {p.Estado}").FontSize(9).Bold().FontColor(Colors.Green.Darken2);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Column(clienteCol =>
                        {
                            clienteCol.Item().Text($"Cliente: {c.RazonSocial}").Bold();
                            clienteCol.Item().Text($"CUIT: {c.Cuit}");
                            if (!string.IsNullOrWhiteSpace(c.Direccion))
                                clienteCol.Item().Text($"Entrega: {c.Direccion} ({c.Localidad})");
                            if (!string.IsNullOrWhiteSpace(c.Telefono))
                                clienteCol.Item().Text($"Tel: {c.Telefono}");
                        });
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        tabla.Header(h =>
                        {
                            h.Cell().Element(CeldaEncabezado).Text("#").Bold();
                            h.Cell().Element(CeldaEncabezado).Text("Producto / Mercadería").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("Cantidad").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("P. Unit.").Bold();
                            h.Cell().Element(CeldaEncabezado).AlignRight().Text("Subtotal").Bold();
                        });

                        var idx = 1;
                        foreach (var d in detalles)
                        {
                            tabla.Cell().Element(CeldaCuerpo).Text($"{idx++}");
                            tabla.Cell().Element(CeldaCuerpo).Text(d.ProductoNombre);
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"{d.Cantidad}");
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"${d.PrecioUnitario:N2}");
                            tabla.Cell().Element(CeldaCuerpo).AlignRight().Text($"${d.Subtotal:N2}").Bold();
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Text($"TOTAL: ${total:N2}").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                    if (!string.IsNullOrWhiteSpace(p.Observaciones))
                    {
                        col.Item().PaddingTop(10).Background(Colors.Grey.Lighten4).Padding(8).Column(obs =>
                        {
                            obs.Item().Text("Observaciones de Entrega:").Bold().FontSize(9);
                            obs.Item().Text(p.Observaciones).FontSize(9);
                        });
                    }
                });

                page.Footer().AlignCenter().Column(f =>
                {
                    f.Item().AlignCenter().Width(75).Image(GenerarQrPedido(p.Numero));
                    f.Item().AlignCenter().Text(p.Numero).FontSize(8).Bold();
                    f.Item().AlignCenter().Text("Escanee este código desde la app móvil para consultar la orden.").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
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

    // Listado de choferes activos para el selector de asignación (CU-20)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpGet("choferes")]
    public async Task<IActionResult> Choferes(CancellationToken ct)
    {
        var choferes = await (
            from u in db.Usuarios.AsNoTracking()
            join r in db.Roles.AsNoTracking() on u.RolId equals r.Id
            where u.Activo && r.Codigo == RolCodigos.Chofer
            orderby u.Nombre
            select new { id = u.Id, nombre = u.Nombre }
        ).ToListAsync(ct);

        return Ok(new { choferes });
    }

    // Asignación o reasignación de chofer (CU-20 / OPERARIO y ADMIN)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
    [HttpPut("{id:long}/chofer")]
    public async Task<IActionResult> AsignarChofer(long id, [FromBody] AsignarChoferRequest request, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado || pedido.Estado == EstadoPedido.Cancelado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "No se puede asignar chofer a un pedido finalizado o cancelado." });

        var chofer = await (
            from u in db.Usuarios
            join r in db.Roles on u.RolId equals r.Id
            where u.Id == request.ChoferId && u.Activo && r.Codigo == RolCodigos.Chofer
            select u
        ).FirstOrDefaultAsync(ct);

        if (chofer is null)
            return BadRequest(new { codigo = "chofer_invalido", mensaje = "El chofer seleccionado no existe, está inactivo o no tiene rol de chofer." });

        pedido.ChoferId = chofer.Id;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Chofer '{chofer.Nombre}' asignado correctamente a la orden {pedido.Numero}.",
            pedido_id = pedido.Id,
            chofer_id = pedido.ChoferId
        });
    }

    // Registro de entrega fallida (CU-21 / CHOFER y ADMIN)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/no-entregado")]
    public async Task<IActionResult> RegistrarEntregaFallida(
        long id,
        [FromBody] EntregaFallidaRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "ENTREGA_FALLIDA", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        if (string.IsNullOrWhiteSpace(request?.Motivo))
            return BadRequest(new { codigo = "motivo_requerido", mensaje = "Debe indicar el motivo de la entrega fallida." });

        if (!MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FotoUrl))
            return BadRequest(new { codigo = "evidencia_invalida", mensaje = "La foto adjunta tiene un formato o URL no permitido." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty,
            Latitud: request.Latitud,
            Longitud: request.Longitud,
            EsSincronizacionOffline: request.EsOffline,
            OcurridoEn: request.OcurridoEn
        );

        var estadoAnterior = pedido.Estado;
        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.NoEntregado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await AvisarConflictosOfflineAsync(
            pedido, estadoAnterior, request.EsOffline, EstadoPedido.NoEntregado, ct);

        var intento = new IntentoEntrega
        {
            PedidoId = pedido.Id,
            ChoferId = User.UsuarioId(),
            Motivo = request.Motivo.Trim(),
            Nota = NormalizarTexto(request.Nota),
            FotoUrl = request.FotoUrl,
            OcurridoEn = request.OcurridoEn ?? DateTime.UtcNow
        };
        db.IntentosEntrega.Add(intento);

        await db.SaveChangesAsync(ct);

        var respuestaObj = new
        {
            mensaje = "Entrega fallida registrada. Pedido en estado NoEntregado.",
            estado = pedido.Estado.ToString(),
            intento_id = intento.Id,
            motivo = intento.Motivo
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotenciaService.RegistrarOperacionAsync(
                idempotencyKey,
                "ENTREGA_FALLIDA",
                User.UsuarioId(),
                pedido.Id,
                System.Text.Json.JsonSerializer.Serialize(respuestaObj),
                ct
            );
        }

        return Ok(respuestaObj);
    }

    // Reintento o reprogramación de entrega (CU-21 / ADMIN)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPost("{id:long}/reintentar-entrega")]
    public async Task<IActionResult> ReintentarEntrega(
        long id,
        [FromBody] ReintentarEntregaRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "REINTENTAR_ENTREGA", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        if (pedido.Estado != EstadoPedido.NoEntregado)
            return BadRequest(new { codigo = "estado_invalido", mensaje = "Solo pedidos en estado NoEntregado admiten reintento de despacho." });

        if (request?.ChoferId.HasValue == true)
        {
            pedido.ChoferId = request.ChoferId.Value;
        }

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty
        );

        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Despachado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Orden {pedido.Numero} reprogramada y despachada nuevamente.",
            estado = pedido.Estado.ToString(),
            chofer_id = pedido.ChoferId
        });
    }

    // Cierre de entrega parcial con ítems rechazados (CU-13 / Decisión 14)
    [Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Chofer}")]
    [HttpPost("{id:long}/entrega-parcial")]
    public async Task<IActionResult> EntregaParcial(
        long id,
        [FromBody] EntregaParcialRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var op = await idempotenciaService.ObtenerOperacionAsync(idempotencyKey, "ENTREGA_PARCIAL", ct);
            if (op?.ResultadoJson != null)
                return Content(op.ResultadoJson, "application/json");
        }

        if (!MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FotoUrl) ||
            !MercuryGo.Api.Utils.ValidadorEvidencia.EsEvidenciaValida(request.FirmaUrl))
        {
            return BadRequest(new { codigo = "evidencia_invalida", mensaje = "La evidencia de entrega (foto o firma) tiene un formato o URL no permitido." });
        }

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue cancelado." });

        var detalles = await db.DetallesPedido.Where(d => d.PedidoId == id).ToListAsync(ct);
        var mapaRechazos = (request.ItemsRechazados ?? []).ToDictionary(r => r.ProductoId);

        foreach (var d in detalles)
        {
            if (mapaRechazos.TryGetValue(d.ProductoId, out var rechazo))
            {
                var cantRechazada = Math.Min(d.Cantidad, Math.Max(0, rechazo.CantidadRechazada));
                d.CantidadRechazada = cantRechazada;
                d.CantidadEntregada = d.Cantidad - cantRechazada;
                d.MotivoRechazo = NormalizarTexto(rechazo.Motivo);

                // Devolver unidades lógicas y ajustar total por los productos rechazados en puerta
                if (cantRechazada > 0)
                {
                    d.Cantidad = d.CantidadEntregada.Value; // Ajuste del total de la orden
                }
            }
            else
            {
                d.CantidadEntregada = d.Cantidad;
                d.CantidadRechazada = 0;
            }
        }

        // Adjuntar prueba de entrega si se envió (CU-23)
        if (!string.IsNullOrWhiteSpace(request.FotoUrl)) pedido.EntregaFotoUrl = request.FotoUrl;
        if (!string.IsNullOrWhiteSpace(request.FirmaUrl)) pedido.EntregaFirmaUrl = request.FirmaUrl;

        var contexto = new MercuryGo.Api.Services.ContextoTransicion(
            UsuarioId: User.UsuarioId(),
            RolCodigo: User.RolCodigo() ?? string.Empty,
            Latitud: request.Latitud,
            Longitud: request.Longitud,
            EsSincronizacionOffline: request.EsOffline,
            OcurridoEn: request.OcurridoEn
        );

        var estadoAnterior = pedido.Estado;
        var resultado = await transicionService.TransicionarAsync(pedido, EstadoPedido.Entregado, contexto, ct);
        if (!resultado.Exito)
            return BadRequest(new { codigo = resultado.CodigoError, mensaje = resultado.MensajeError });

        await AvisarConflictosOfflineAsync(
            pedido, estadoAnterior, request.EsOffline, EstadoPedido.Entregado, ct);

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Entrega registrada con ajustes por ítems rechazados.",
            estado = pedido.Estado.ToString(),
            total_ajustado = detalles.Sum(d => d.Cantidad * d.PrecioUnitario)
        });
    }

    // Ajuste manual de cobro por parte del Administrador (Decisión 4)
    [Authorize(Roles = RolCodigos.Admin)]
    [HttpPut("cobros/{cobroId:long}/ajustar")]
    public async Task<IActionResult> AjustarCobro(long cobroId, [FromBody] AjustarCobroRequest request, CancellationToken ct)
    {
        var cobro = await db.CobrosPedido.FirstOrDefaultAsync(c => c.Id == cobroId, ct);
        if (cobro is null)
            return NotFound(new { codigo = "cobro_no_encontrado", mensaje = "El cobro no existe." });

        if (request.NuevoMonto < 0)
            return BadRequest(new { codigo = "monto_invalido", mensaje = "El monto ajustado no puede ser negativo." });

        cobro.Monto = request.NuevoMonto;
        if (!string.IsNullOrWhiteSpace(request.Metodo)) cobro.Metodo = request.Metodo.Trim();
        cobro.RecibidoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Cobro ajustado a ${cobro.Monto:N2}.",
            cobro_id = cobro.Id,
            monto = cobro.Monto,
            metodo = cobro.Metodo
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

    private static IContainer CeldaEncabezado(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5);

    private static IContainer CeldaCuerpo(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);

    private static byte[] GenerarQrPedido(string numeroRemito)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(numeroRemito, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }

    private static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}

public sealed record CrearPedidoItemRequest(long ProductoId, int Cantidad);
public sealed record CrearPedidoRequest(long ClienteId, string? Observaciones, List<CrearPedidoItemRequest> Items, string? Estado = null);
public sealed record EditarPedidoRequest(List<CrearPedidoItemRequest>? Items, string? Observaciones);
public sealed record CobroChoferRequest(decimal Monto, string? Metodo = "Efectivo", DateTime? OcurridoEn = null);
public sealed record AprobarComprobanteRequest(decimal? Monto);
public sealed record RechazarComprobanteRequest(string? Motivo);
public sealed record CambiarEstadoRequest(string Estado, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null, string? FotoUrl = null, string? FirmaUrl = null);
public sealed record ItemPickingRequest(long ProductoId, string EstadoItem, int CantidadPreparada);
public sealed record ActualizarPickingRequest(List<ItemPickingRequest> Items, bool EsOffline = false);

public sealed record SolicitarCancelacionRequest(string? Motivo);
public sealed record ResolverCancelacionRequest(bool Aprobada);
public sealed record AsignarChoferRequest(long ChoferId);
public sealed record EntregaFallidaRequest(string Motivo, string? Nota = null, string? FotoUrl = null, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null);
public sealed record ReintentarEntregaRequest(long? ChoferId = null);
public sealed record ItemRechazadoRequest(long ProductoId, int CantidadRechazada, string? Motivo = null);
public sealed record EntregaParcialRequest(List<ItemRechazadoRequest>? ItemsRechazados, string? FotoUrl = null, string? FirmaUrl = null, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null);
public sealed record AjustarCobroRequest(decimal NuevoMonto, string? Metodo = null);

public sealed record PedidoListaResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    long ClienteId,
    string ClienteRazonSocial,
    decimal Total,
    string? Observaciones
);

public sealed record PedidoDetalleResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    string? Observaciones,
    ClientePedidoResponse Cliente,
    IEnumerable<DetallePedidoResponse> Detalles,
    decimal Total,
    double? LatitudEntrega = null,
    double? LongitudEntrega = null,
    DateTime? FechaEnPuerta = null,
    long? ChoferId = null,
    string? PickingResultado = null,
    string? ComprobanteEstado = null,
    string? ComprobanteUrl = null,
    string? CancelacionEstado = null,
    bool ValidacionFallida = false,
    string? ValidacionDetalle = null,
    bool ItemsEditables = true,
    bool ObservacionesEditables = true
);

public sealed record ClientePedidoResponse(long Id, string RazonSocial, string Cuit);

public sealed record DetallePedidoResponse(
    long Id,
    long ProductoId,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    int CantidadPedida = 0,
    int CantidadPreparada = 0,
    int? CantidadEntregada = null,
    int? CantidadRechazada = null,
    string? MotivoRechazo = null
);
