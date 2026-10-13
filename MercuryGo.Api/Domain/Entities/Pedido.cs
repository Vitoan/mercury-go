namespace MercuryGo.Api.Domain.Entities;

// Estado del pedido: el orden importa porque refleja el flujo real de depósito.
// Pendiente → Confirmado → EnPreparacion → Despachado → Entregado
// Cancelado puede ocurrir desde Pendiente o Confirmado solamente.
public enum EstadoPedido
{
    Pendiente,
    Confirmado,
    EnPreparacion,
    Despachado,
    EnPuerta,
    Entregado,
    Cancelado
}

public sealed class Pedido : EntityBase
{
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Número legible por humanos: "PED-000001". El Id es la clave técnica,
    // el Numero es lo que aparece en remitos y pantallas de operación.
    public string Numero { get; set; } = string.Empty;
    public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;
    public DateTime FechaPedido { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; } = true;

    // Coordenadas GPS y fecha de llegada capturadas por el chofer al llegar al destino (Semana 7)
    public double? LatitudEntrega { get; set; }
    public double? LongitudEntrega { get; set; }
    public DateTime? FechaEnPuerta { get; set; }

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}
