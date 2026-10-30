using System;
using System.Collections.Generic;

namespace MercuryGo.Api.Controllers;

public sealed record CrearPedidoItemRequest(long ProductoId, int Cantidad, decimal? PrecioUnitario = null);
public sealed record CrearPedidoRequest(long ClienteId, string? Observaciones, List<CrearPedidoItemRequest> Items, string? Estado = null);
public sealed record EditarPedidoRequest(List<CrearPedidoItemRequest>? Items, string? Observaciones);
public sealed record CobroChoferRequest(decimal Monto, string? Metodo = "Efectivo", DateTime? OcurridoEn = null);
public sealed record AprobarComprobanteRequest(decimal? Monto);
public sealed record RechazarComprobanteRequest(string? Motivo);
public sealed record CambiarEstadoRequest(string Estado, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null, string? FotoUrl = null, string? FirmaUrl = null);
public sealed record ItemPickingRequest(long ProductoId, string EstadoItem, int CantidadPreparada);
public sealed record ActualizarPickingRequest(List<ItemPickingRequest> Items, bool EsOffline = false);

public sealed record SolicitarCancelacionRequest(string? Motivo);
public sealed record ResolverCancelacionRequest(bool Aprobada);
public sealed record AsignarChoferRequest(long ChoferId);
public sealed record EntregaFallidaRequest(string Motivo, string? Nota = null, string? FotoUrl = null, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null);
public sealed record ReintentarEntregaRequest(long? ChoferId = null);
public sealed record ItemRechazadoRequest(long ProductoId, int CantidadRechazada, string? Motivo = null);
public sealed record EntregaParcialRequest(List<ItemRechazadoRequest>? ItemsRechazados, string? FotoUrl = null, string? FirmaUrl = null, double? Latitud = null, double? Longitud = null, bool EsOffline = false, DateTime? OcurridoEn = null);
public sealed record AjustarCobroRequest(decimal NuevoMonto, string? Metodo = null);

public sealed record PedidoListaResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    long ClienteId,
    string ClienteRazonSocial,
    decimal Total,
    string? Observaciones
);

public sealed record PedidoDetalleResponse(
    long Id,
    string Numero,
    string Estado,
    DateTime FechaPedido,
    string? Observaciones,
    ClientePedidoResponse Cliente,
    IEnumerable<DetallePedidoResponse> Detalles,
    decimal Total,
    double? LatitudEntrega = null,
    double? LongitudEntrega = null,
    DateTime? FechaEnPuerta = null,
    long? ChoferId = null,
    string? PickingResultado = null,
    string? ComprobanteEstado = null,
    string? ComprobanteUrl = null,
    string? CancelacionEstado = null,
    bool ValidacionFallida = false,
    string? ValidacionDetalle = null,
    bool ItemsEditables = true,
    bool ObservacionesEditables = true
);

public sealed record ClientePedidoResponse(long Id, string RazonSocial, string Cuit);

public sealed record DetallePedidoResponse(
    long Id,
    long ProductoId,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    int CantidadPedida = 0,
    int CantidadPreparada = 0,
    int? CantidadEntregada = null,
    int? CantidadRechazada = null,
    string? MotivoRechazo = null
);
