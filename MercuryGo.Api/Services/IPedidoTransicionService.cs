using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

public sealed record ContextoTransicion(
    long UsuarioId,
    string RolCodigo,
    double? Latitud = null,
    double? Longitud = null,
    bool EsSincronizacionOffline = false,
    DateTime? OcurridoEn = null
);

public sealed record ResultadoTransicion(
    bool Exito,
    string? CodigoError = null,
    string? MensajeError = null,
    EstadoPedido? EstadoAnterior = null,
    EstadoPedido? NuevoEstado = null
)
{
    public static ResultadoTransicion Correcto(EstadoPedido anterior, EstadoPedido nuevo) =>
        new(true, null, null, anterior, nuevo);

    public static ResultadoTransicion Fallo(string codigo, string mensaje) =>
        new(false, codigo, mensaje, null, null);
}

public interface IPedidoTransicionService
{
    Task<ResultadoTransicion> TransicionarAsync(
        Pedido pedido,
        EstadoPedido nuevoEstado,
        ContextoTransicion contexto,
        CancellationToken ct = default
    );
}
