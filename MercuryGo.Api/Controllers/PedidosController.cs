using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario},{RolCodigos.Chofer},{RolCodigos.Cliente}")]
[Route("api/pedidos")]
public sealed class PedidosController(MercuryGoDbContext db) : ControllerBase
{
    // Diccionario de la máquina de estados de un pedido logístico.
    // La validación vive estrictamente en el servidor.
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> TransicionesValidas = new()
    {
        [EstadoPedido.Pendiente] = [EstadoPedido.Confirmado, EstadoPedido.Cancelado],
        [EstadoPedido.Confirmado] = [EstadoPedido.EnPreparacion, EstadoPedido.Cancelado],
        [EstadoPedido.EnPreparacion] = [EstadoPedido.Despachado, EstadoPedido.Cancelado],
        [EstadoPedido.Despachado] = [EstadoPedido.Entregado, EstadoPedido.Cancelado],
        [EstadoPedido.Entregado] = [], // Estado final: no admite cambios posteriores
        [EstadoPedido.Cancelado] = []  // Estado final
    };

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
                    2)
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
            total
        );

        return Ok(new { pedido = resultado });
    }

    // Creación de orden de carga: LOS PRECIOS NO VIAJAN DESDE EL TELÉFONO.
    // El servidor lee los precios vigentes de la base de datos para preservar la integridad contable.
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearPedidoRequest request, CancellationToken ct)
    {
        if (request.ClienteId <= 0)
            return BadRequest(new { codigo = "cliente_requerido", mensaje = "Debe especificar un cliente comercial válido." });

        var clienteExiste = await db.Clientes.AnyAsync(c => c.Id == request.ClienteId && c.Activo, ct);
        if (!clienteExiste)
            return BadRequest(new { codigo = "cliente_no_encontrado", mensaje = "El cliente seleccionado no existe o está inactivo." });

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { codigo = "items_requeridos", mensaje = "La orden de carga debe contener al menos un producto." });

        var productosIds = request.Items.Select(i => i.ProductoId).Distinct().ToList();
        var productosDb = await db.Productos
            .Where(p => productosIds.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in request.Items)
        {
            if (!productosDb.TryGetValue(item.ProductoId, out var prod))
                return BadRequest(new { codigo = "producto_invalido", mensaje = $"El producto con ID {item.ProductoId} no existe o está inactivo." });

            if (item.Cantidad <= 0)
                return BadRequest(new { codigo = "cantidad_invalida", mensaje = $"La cantidad para '{prod.Nombre}' debe ser mayor a cero." });
        }

        // Generar número de remito correlativo
        var totalPedidos = await db.Pedidos.CountAsync(ct);
        var numeroRemito = $"PED-{(totalPedidos + 1):D6}";

        var nuevoPedido = new Pedido
        {
            ClienteId = request.ClienteId,
            Numero = numeroRemito,
            Estado = EstadoPedido.Pendiente,
            FechaPedido = DateTime.UtcNow,
            Observaciones = NormalizarTexto(request.Observaciones),
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.Pedidos.Add(nuevoPedido);
        await db.SaveChangesAsync(ct);

        foreach (var item in request.Items)
        {
            var prod = productosDb[item.ProductoId];
            db.DetallesPedido.Add(new DetallePedido
            {
                PedidoId = nuevoPedido.Id,
                ProductoId = prod.Id,
                Cantidad = item.Cantidad,
                PrecioUnitario = prod.Precio, // Precio histórico congelado al crear
                CreadoEn = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { pedido_id = nuevoPedido.Id, numero = nuevoPedido.Numero });
    }

    // Transición de estado evaluada con la máquina de estados del servidor.
    [HttpPut("{id:long}/estado")]
    public async Task<IActionResult> CambiarEstado(long id, [FromBody] CambiarEstadoRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<EstadoPedido>(request.Estado, true, out var nuevoEstado))
            return BadRequest(new { codigo = "estado_invalido", mensaje = $"El estado '{request.Estado}' no es un estado logístico reconocido." });

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue dado de baja." });

        if (pedido.Estado == nuevoEstado)
            return Ok(new { estado = pedido.Estado.ToString() });

        if (!TransicionesValidas.TryGetValue(pedido.Estado, out var destinosPermitidos) || !destinosPermitidos.Contains(nuevoEstado))
        {
            return BadRequest(new
            {
                codigo = "transicion_invalida",
                mensaje = $"No es posible cambiar el estado de '{pedido.Estado}' a '{nuevoEstado}'. Transición rechazada por el servidor."
            });
        }

        pedido.Estado = nuevoEstado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { estado = pedido.Estado.ToString() });
    }

    // Cancelación lógica de una orden de carga
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (pedido is null)
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o ya fue cancelado." });

        if (pedido.Estado == EstadoPedido.Entregado)
            return BadRequest(new { codigo = "pedido_ya_entregado", mensaje = "Una orden entregada no puede cancelarse." });

        pedido.Estado = EstadoPedido.Cancelado;
        pedido.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { cancelado = true });
    }

    private static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}

public sealed record CrearPedidoItemRequest(long ProductoId, int Cantidad);
public sealed record CrearPedidoRequest(long ClienteId, string? Observaciones, List<CrearPedidoItemRequest> Items);
public sealed record CambiarEstadoRequest(string Estado);

public sealed record PedidoListaResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    long ClienteId,
    string ClienteRazonSocial,
    decimal Total
);

public sealed record PedidoDetalleResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    string? Observaciones,
    ClientePedidoResponse Cliente,
    IEnumerable<DetallePedidoResponse> Detalles,
    decimal Total
);

public sealed record ClientePedidoResponse(long Id, string RazonSocial, string Cuit);

public sealed record DetallePedidoResponse(
    long Id,
    long ProductoId,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal
);
