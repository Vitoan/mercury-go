using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Common;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;

namespace MercuryGo.Api.Controllers;

public abstract class PedidosControllerBase(
    MercuryGoDbContext db,
    ConflictoOfflineService? conflictoOfflineService = null) : ControllerBase
{
    protected readonly MercuryGoDbContext db = db;
    protected readonly ConflictoOfflineService? conflictoOfflineService = conflictoOfflineService;

    protected Task<Cliente?> ObtenerClienteDeUsuarioAutenticadoAsync(CancellationToken ct) =>
        db.ResolverClienteDeUsuarioAsync(User.UsuarioId(), User.Email(), ct);

    protected async Task<bool> PuedeAccederAlPedidoAsync(Pedido pedido, CancellationToken ct)
    {
        if (User.IsInRole(RolCodigos.Admin) || User.IsInRole(RolCodigos.Operario))
            return true;

        if (User.IsInRole(RolCodigos.Chofer))
        {
            var usuarioId = User.UsuarioId();
            return (pedido.ChoferId.HasValue && pedido.ChoferId.Value == usuarioId)
                || pedido.Estado == EstadoPedido.Despachado;
        }

        if (User.EsCliente())
        {
            var cliente = await ObtenerClienteDeUsuarioAutenticadoAsync(ct);
            return cliente != null && pedido.ClienteId == cliente.Id;
        }

        return false;
    }

    // Avisos al Admin cuando una acción tomada sin red choca con lo que pasó
    // entretanto en el servidor (Decisiones 10 y 17). La acción se aplica igual
    // —gana el chofer— pero el Admin tiene que enterarse.
    protected async Task AvisarConflictosOfflineAsync(
        Pedido pedido,
        EstadoPedido estadoAnterior,
        bool esOffline,
        EstadoPedido estadoNuevo,
        CancellationToken ct)
    {
        if (!esOffline || conflictoOfflineService is null)
            return;

        var usuarioId = User.UsuarioId();
        var esChofer = string.Equals(User.RolCodigo(), RolCodigos.Chofer, StringComparison.OrdinalIgnoreCase);

        var avisosCancelacion = await conflictoOfflineService.AvisarEntregaSobreCanceladoAsync(
            pedido, usuarioId, estadoAnterior, ct);

        if (avisosCancelacion > 0)
            return;

        await conflictoOfflineService.AvisarChoferReasignadoAsync(
            pedido, usuarioId, esChofer, estadoNuevo, ct);
    }

    protected static string? NormalizarTexto(string? texto) => TextoUtil.Normalizar(texto);
}
