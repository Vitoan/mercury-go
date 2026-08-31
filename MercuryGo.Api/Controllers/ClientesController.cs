using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(MercuryGoDbContext db) : ControllerBase
{
    // Lista de clientes activos. En B2B la lista suele ser corta (decenas, no miles),
    // por eso va toda junta. Si el negocio crece, se pagina en v4.
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var clientes = await db.Clientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .Select(c => new ClienteListaResponse(
                c.Id,
                c.RazonSocial,
                c.Cuit,
                c.Localidad,
                c.Telefono,
                c.Email
            ))
            .ToListAsync(ct);

        return Ok(new { total = clientes.Count, clientes });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken ct)
    {
        var cliente = await db.Clientes
            .AsNoTracking()
            .Where(c => c.Id == id && c.Activo)
            .Select(c => new ClienteDetalleResponse(
                c.Id,
                c.RazonSocial,
                c.Cuit,
                c.Direccion,
                c.Localidad,
                c.Telefono,
                c.Email,
                // Conteo de pedidos activos del cliente (subconsulta resuelta en la base)
                db.Pedidos.Count(p => p.ClienteId == c.Id && p.Activo)
            ))
            .FirstOrDefaultAsync(ct);

        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El cliente no existe o fue dado de baja." });

        return Ok(new { cliente });
    }
}

public sealed record ClienteListaResponse(
    long Id,
    string RazonSocial,
    string Cuit,
    string? Localidad,
    string? Telefono,
    string? Email
);

public sealed record ClienteDetalleResponse(
    long Id,
    string RazonSocial,
    string Cuit,
    string? Direccion,
    string? Localidad,
    string? Telefono,
    string? Email,
    int TotalPedidos
);
