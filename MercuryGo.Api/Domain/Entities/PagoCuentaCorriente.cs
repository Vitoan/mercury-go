namespace MercuryGo.Api.Domain.Entities;

public sealed class PagoCuentaCorriente : EntityBase
{
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public decimal Monto { get; set; }
    public string? ComprobanteUrl { get; set; }
    public string Estado { get; set; } = "Pendiente"; // Pendiente, Aprobado, Rechazado
    public string? Observaciones { get; set; }
    public string? MotivoRechazo { get; set; }

    public long? AuditadoPorId { get; set; }
    public Usuario? AuditadoPor { get; set; }
    public DateTime? AuditadoEn { get; set; }

    public bool Activo { get; set; } = true;
}
