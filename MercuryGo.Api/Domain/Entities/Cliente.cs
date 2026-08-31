namespace MercuryGo.Api.Domain.Entities;

// Cliente B2B: almacén, supermercado o distribuidor que hace pedidos a MercuryGO.
// El CUIT identifica al cliente en Argentina; se guarda como texto para no perder ceros iniciales.
public sealed class Cliente : EntityBase
{
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
