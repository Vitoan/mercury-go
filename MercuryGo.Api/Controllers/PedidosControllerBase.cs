using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

public abstract class PedidosControllerBase(MercuryGoDbContext db) : ControllerBase
{
    protected readonly MercuryGoDbContext db = db;

    protected async Task<Cliente?> ObtenerClienteDeUsuarioAutenticadoAsync(CancellationToken ct)
    {
        var usuarioId = User.UsuarioId();
        if (usuarioId > 0)
        {
            var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo, ct);
            if (usuario?.ClienteId != null)
            {
                var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == usuario.ClienteId.Value && c.Activo, ct);
                if (cliente != null) return cliente;
            }
        }

        var emailUsuario = User.Email();
        if (!string.IsNullOrWhiteSpace(emailUsuario))
        {
            return await db.Clientes.FirstOrDefaultAsync(c => c.Email == emailUsuario && c.Activo, ct);
        }

        return null;
    }

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

    protected static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}
