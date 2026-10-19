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
    NoEntregado,
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

    // Asignación de chofer (CU-20)
    public long? ChoferId { get; set; }
    public Usuario? Chofer { get; set; }

    // Confirmación del pedido (CU-19 / Decisión 1)
    public long? ConfirmadoPorId { get; set; }
    public Usuario? ConfirmadoPor { get; set; }
    public DateTime? ConfirmadoEn { get; set; }

    // Picking / preparación (CU-11 / Decisión 5 y 9)
    public string? PickingResultado { get; set; } // "Completo" | "Incompleto"
    public DateTime? PickingEn { get; set; }

    // Comprobante de transferencia bancaria (CU-14 y CU-15)
    public string? ComprobanteEstado { get; set; } // "SinComprobante" | "Pendiente" | "Aprobado" | "Rechazado"
    public string? ComprobanteUrl { get; set; }
    public long? AuditadoPorId { get; set; }
    public Usuario? AuditadoPor { get; set; }
    public DateTime? AuditadoEn { get; set; }

    // Entrega y Prueba de Entrega (CU-12, CU-13, CU-23 / Decisión 15 y 21)
    public double? LatitudEntrega { get; set; }
    public double? LongitudEntrega { get; set; }
    public DateTime? FechaEnPuerta { get; set; }
    public DateTime? EntregadoEn { get; set; }
    public string? EntregaFotoUrl { get; set; }
    public string? EntregaFirmaUrl { get; set; }

    // Solicitud y resolución de cancelación (CU-09 / Decisión 12)
    public long? CancelacionSolicitadaPorId { get; set; }
    public Usuario? CancelacionSolicitadaPor { get; set; }
    public string? CancelacionMotivo { get; set; }
    public string? CancelacionEstado { get; set; } // "Ninguna" | "Solicitada" | "Aprobada" | "Rechazada"

    // Creación offline y validación pendiente (Decisión 18 y 19)
    public bool ValidacionFallida { get; set; } = false;
    public string? ValidacionDetalle { get; set; }

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public ICollection<CobroPedido> Cobros { get; set; } = new List<CobroPedido>();
    public ICollection<IntentoEntrega> IntentosEntrega { get; set; } = new List<IntentoEntrega>();
}
