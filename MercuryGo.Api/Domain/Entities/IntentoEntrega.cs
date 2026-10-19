namespace MercuryGo.Api.Domain.Entities;

public sealed class IntentoEntrega : EntityBase
{
    public long PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public long? ChoferId { get; set; }
    public Usuario? Chofer { get; set; }

    public string Motivo { get; set; } = string.Empty;
    public string? Nota { get; set; }
    public string? FotoUrl { get; set; }

    public DateTime OcurridoEn { get; set; }
}
