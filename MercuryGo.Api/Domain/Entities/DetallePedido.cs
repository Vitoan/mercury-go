namespace MercuryGo.Api.Domain.Entities;

// Línea de un pedido: qué producto, cuántas unidades y a qué precio.
// El precio se guarda en el detalle y NO se toma del producto en tiempo real:
// si el precio del producto cambia mañana, el pedido de hoy debe mantener
// el valor pactado. Este patrón se llama "precio histórico".
public sealed class DetallePedido : EntityBase
{
    public long PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public long ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int Cantidad { get; set; }

    // Precio al momento de confirmar el pedido (precio histórico).
    public decimal PrecioUnitario { get; set; }
}
