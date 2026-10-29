using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

// Avisos al Admin cuando una acción tomada sin red choca con lo que pasó
// entretanto en el servidor (Decisiones 10, 16 y 17 del documento de casos de
// uso). La acción del chofer gana igual, pero el Admin tiene que enterarse.
//
// Cada método decide si el caso aplica y devuelve cuántos avisos dejó; así la
// regla vive en un solo lugar testeable y el controller solo la invoca.
// Igual que NotificacionService, no hace SaveChanges: participa de la unidad de
// trabajo del controller.
public sealed class ConflictoOfflineService(MercuryGoDbContext db, NotificacionService notificacionService)
{
    public async Task<int> AvisarEntregaSobreCanceladoAsync(
        Pedido pedido,
        long usuarioId,
        EstadoPedido estadoAnterior,
        CancellationToken ct = default)
    {
        if (estadoAnterior != EstadoPedido.Cancelado || pedido.Estado != EstadoPedido.Entregado)
            return 0;

        var quien = await NombreAsync(usuarioId, ct);
        return await NotificarAsync(
            pedido,
            $"⚠️ Entrega offline sobre pedido cancelado ({pedido.Numero})",
            $"La orden {pedido.Numero} ya estaba cancelada, pero {quien} registró la entrega sin conexión. Se respetó la acción del chofer.",
            ct);
    }

    // Decisión 17: si el pedido se reasignó mientras el chofer estaba sin red, su
    // entrega vale igual, pero el Admin tiene que saber quién la hizo.
    public async Task<int> AvisarChoferReasignadoAsync(
        Pedido pedido,
        long usuarioId,
        bool esChofer,
        EstadoPedido estadoNuevo,
        CancellationToken ct = default)
    {
        var esCierre = estadoNuevo is EstadoPedido.Entregado or EstadoPedido.NoEntregado;

        if (!esChofer || !esCierre || !pedido.ChoferId.HasValue || pedido.ChoferId.Value == usuarioId)
            return 0;

        var ejecutor = await NombreAsync(usuarioId, ct);
        var asignado = await NombreAsync(pedido.ChoferId.Value, ct);
        return await NotificarAsync(
            pedido,
            $"🔀 Entrega offline de un pedido reasignado ({pedido.Numero})",
            $"La orden {pedido.Numero} estaba asignada a {asignado}, pero {ejecutor} la entregó sin conexión. Se respetó la acción del chofer.",
            ct);
    }

    // Decisión 16: el picking se guarda aunque la orden esté cancelada, porque el
    // trabajo físico ya se hizo; la orden sigue cancelada.
    public async Task<int> AvisarPickingSobreCanceladoAsync(
        Pedido pedido,
        long usuarioId,
        bool conFaltantes,
        bool esOffline,
        CancellationToken ct = default)
    {
        if (!esOffline || pedido.Estado != EstadoPedido.Cancelado)
            return 0;

        var quien = await NombreAsync(usuarioId, ct);
        var detalle = conFaltantes ? " El picking quedó registrado con faltantes." : string.Empty;

        return await NotificarAsync(
            pedido,
            $"⚠️ Picking offline sobre pedido cancelado ({pedido.Numero})",
            $"La orden {pedido.Numero} ya estaba cancelada cuando {quien} confirmó el picking sin conexión. Se guardó la preparación y la orden sigue cancelada.{detalle}",
            ct);
    }

    private Task<int> NotificarAsync(
        Pedido pedido,
        string titulo,
        string mensaje,
        CancellationToken ct)
        => notificacionService.NotificarRolAsync(
            RolCodigos.Admin,
            titulo,
            mensaje,
            "PEDIDO",
            pedido.Id,
            ct);

    private async Task<string> NombreAsync(long usuarioId, CancellationToken ct)
    {
        var nombre = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Nombre)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(nombre) ? $"el usuario #{usuarioId}" : nombre;
    }
}
