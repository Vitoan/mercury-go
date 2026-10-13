namespace MercuryGo.Api.Domain.Entities;

public sealed class Notificacion : EntityBase
{
    public long UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Tipo { get; set; } = "INFO"; // INFO, PEDIDO, PAGO, GPS
    public bool Leida { get; set; } = false;
    public long? ReferenciaId { get; set; } // Id del Pedido u otro recurso asociado
}
