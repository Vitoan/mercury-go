namespace MercuryGo.Api.Domain.Entities;

public sealed class Producto : EntityBase
{
    public long CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenUrl { get; set; }
    public int StockActual { get; set; } = 100;
    public int StockMinimo { get; set; } = 10;
    public bool Disponible { get; set; } = true;
    public bool EnOferta { get; set; } = false;
    public bool Activo { get; set; } = true;
}
