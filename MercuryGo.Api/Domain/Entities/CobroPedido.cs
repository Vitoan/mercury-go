namespace MercuryGo.Api.Domain.Entities;

public sealed class CobroPedido : EntityBase
{
    public long PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public decimal Monto { get; set; }
    public string Metodo { get; set; } = "Efectivo";

    public long? RegistradoPorId { get; set; }
    public Usuario? RegistradoPor { get; set; }

    public DateTime OcurridoEn { get; set; }
    public DateTime RecibidoEn { get; set; } = DateTime.UtcNow;
}
