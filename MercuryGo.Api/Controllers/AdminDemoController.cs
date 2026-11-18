using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

/// <summary>
/// Controlador administrativo para restablecer los datos del recorrido de demostración.
/// Protegido exclusivamente para rol ADMIN.
/// </summary>
[ApiController]
[Authorize(Roles = RolCodigos.Admin)]
[Route("api/admin")]
public sealed class AdminDemoController(MercuryGoDbContext db) : ControllerBase
{
    /// <summary>
    /// Restaura stock suficiente en productos activos y vuelve los pedidos de reserva
    /// (PED-000002, PED-000003 y PED-000005) a su estado inicial de demostración.
    /// </summary>
    [HttpPost("reset-demo")]
    public async Task<IActionResult> ResetDemo(CancellationToken ct)
    {
        var productos = await db.Productos
            .Where(p => p.Id == 1 || p.Id == 2 || p.Id == 4 || p.Id == 5 || p.Id == 6)
            .ToListAsync(ct);

        foreach (var producto in productos)
        {
            producto.StockActual = 150;
            producto.Disponible = true;
            producto.Activo = true;
        }

        var pedidos = await db.Pedidos
            .Where(p => p.Id == 2 || p.Id == 3 || p.Id == 5)
            .ToListAsync(ct);

        var pedidosRestaurados = new List<object>();

        foreach (var pedido in pedidos)
        {
            switch (pedido.Id)
            {
                case 2:
                    pedido.ChoferId = 3;
                    pedido.Estado = EstadoPedido.Despachado;
                    pedido.Activo = true;
                    break;
                case 3:
                    pedido.Estado = EstadoPedido.Confirmado;
                    pedido.ChoferId = null;
                    pedido.Activo = true;
                    break;
                case 5:
                    pedido.ClienteId = 1;
                    pedido.Estado = EstadoPedido.Pendiente;
                    pedido.ChoferId = null;
                    pedido.Activo = true;
                    break;
            }

            pedidosRestaurados.Add(new
            {
                id = pedido.Id,
                numero = pedido.Numero,
                estado = pedido.Estado.ToString(),
                cliente_id = pedido.ClienteId,
                chofer_id = pedido.ChoferId
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = "Datos de recorrido de demo restablecidos correctamente.",
            productos_actualizados = productos.Count,
            pedidos_restaurados = pedidosRestaurados
        });
    }
}
