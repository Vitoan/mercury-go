using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
public sealed class PedidosController(MercuryGoDbContext db) : ControllerBase
{
    // Resumen general: conteo por estado + lista de pedidos con info del cliente.
    // El frontend lo usa para mostrar el tablero de operaciones.
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken ct)
    {
        // Conteos por estado agrupados en la base, no en memoria.
        var conteosPorEstado = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.Estado)
            .Select(g => new { estado = g.Key.ToString(), cantidad = g.Count() })
            .ToListAsync(ct);

        // Lista de pedidos con razón social del cliente (join resuelto en la base).
        var pedidos = await (
            from pedido in db.Pedidos.AsNoTracking()
            join cliente in db.Clientes.AsNoTracking() on pedido.ClienteId equals cliente.Id
            where pedido.Activo
            orderby pedido.FechaPedido descending
            select new PedidoListaResponse(
                pedido.Id,
                pedido.Numero,
                pedido.Estado.ToString(),
                pedido.FechaPedido,
                cliente.Id,
                cliente.RazonSocial,
                // Total del pedido: suma de (cantidad × precio) de sus líneas.
                Math.Round(
                    db.DetallesPedido
                        .Where(d => d.PedidoId == pedido.Id)
                        .Sum(d => (decimal?)d.Cantidad * d.PrecioUnitario) ?? 0m,
                    2)
            )
        ).ToListAsync(ct);

        return Ok(new { conteos_por_estado = conteosPorEstado, total = pedidos.Count, pedidos });
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
            return NotFound(new { codigo = "pedido_no_encontrado", mensaje = "El pedido no existe o fue dado de baja." });

        // Líneas del pedido con nombre de producto (join independiente del pedido principal).
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

        var total = detalles.Sum(d => d.Subtotal);

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
}

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
