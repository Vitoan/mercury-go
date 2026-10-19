namespace MercuryGo.Api.Domain.Entities;

public sealed class Categoria : EntityBase
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
