using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

// Ítem solicitado en una edición, desacoplado del DTO HTTP para poder probar la
// reconciliación sin levantar el controller.
public readonly record struct ItemEdicionPedido(long ProductoId, int Cantidad);

public sealed record ErrorEdicionPedido(string Codigo, string Mensaje);

public sealed record ResultadoEdicionPedido(
    IReadOnlyList<DetallePedido> Agregados,
    IReadOnlyList<DetallePedido> Eliminados,
    decimal TotalResultante,
    ErrorEdicionPedido? Error);

// Edición de las líneas de un pedido sin borrar y reinsertar todo.
// Borrar los detalles perdía el precio histórico, la cantidad preparada y las
// cantidades entregadas/rechazadas; acá se actualiza cada línea en su lugar.
public static class PedidoEdicionService
{
    public static ResultadoEdicionPedido Reconciliar(
        long pedidoId,
        IReadOnlyList<DetallePedido> actuales,
        IReadOnlyList<ItemEdicionPedido> solicitados,
        IReadOnlyDictionary<long, Producto> productos)
    {
        static ResultadoEdicionPedido Falla(string codigo, string mensaje) =>
            new([], [], 0m, new ErrorEdicionPedido(codigo, mensaje));

        if (solicitados is null || solicitados.Count == 0)
            return Falla("items_requeridos", "La orden debe conservar al menos un producto.");

        var repetido = solicitados
            .GroupBy(i => i.ProductoId)
            .FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            return Falla("items_duplicados", $"El producto {repetido.Key} está repetido en la edición.");

        var sinCantidad = solicitados.FirstOrDefault(i => i.Cantidad <= 0);
        if (solicitados.Any(i => i.Cantidad <= 0))
            return Falla("cantidad_invalida", $"La cantidad del producto {sinCantidad.ProductoId} debe ser mayor a cero.");

        foreach (var item in solicitados)
        {
            if (!productos.TryGetValue(item.ProductoId, out var producto) || !producto.Activo)
                return Falla("producto_invalido", $"El producto {item.ProductoId} no existe o está inactivo.");
        }

        var actualesPorProducto = actuales
            .GroupBy(d => d.ProductoId)
            .ToDictionary(g => g.Key, g => g.First());
        var solicitadosPorProducto = solicitados.ToDictionary(i => i.ProductoId);

        var agregados = new List<DetallePedido>();

        foreach (var item in solicitados)
        {
            if (actualesPorProducto.TryGetValue(item.ProductoId, out var existente))
            {
                existente.Cantidad = item.Cantidad;
                existente.CantidadPedida = item.Cantidad;

                // Si la nueva cantidad es menor a lo ya preparado, se recorta para
                // no dejar preparado más de lo pedido.
                if (existente.CantidadPreparada > item.Cantidad)
                    existente.CantidadPreparada = item.Cantidad;
            }
            else
            {
                var producto = productos[item.ProductoId];
                agregados.Add(new DetallePedido
                {
                    PedidoId = pedidoId,
                    ProductoId = producto.Id,
                    Cantidad = item.Cantidad,
                    CantidadPedida = item.Cantidad,
                    CantidadPreparada = item.Cantidad,
                    PrecioUnitario = producto.Precio,
                    CreadoEn = DateTime.UtcNow
                });
            }
        }

        var eliminados = actuales
            .Where(d => !solicitadosPorProducto.ContainsKey(d.ProductoId))
            .ToList();

        var total = actuales
            .Where(d => !eliminados.Contains(d))
            .Sum(d => d.Cantidad * d.PrecioUnitario)
            + agregados.Sum(d => d.Cantidad * d.PrecioUnitario);

        return new ResultadoEdicionPedido(agregados, eliminados, total, null);
    }
}
