namespace MercuryGo.Api.Domain.Entities;

public sealed class OperacionProcesada
{
    // Clave primaria técnica: el UUID v4 de la acción enviado en el header Idempotency-Key
    public string Id { get; set; } = string.Empty;

    public long? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Tipo { get; set; } = string.Empty;
    public long? PedidoId { get; set; }

    // Payload de respuesta JSON cacheado para devolver exactamente el mismo resultado al reintentar
    public string? ResultadoJson { get; set; }

    public DateTime ProcesadaEn { get; set; } = DateTime.UtcNow;
}
