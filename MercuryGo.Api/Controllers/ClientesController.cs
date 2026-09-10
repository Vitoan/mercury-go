using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{RolCodigos.Admin},{RolCodigos.Operario}")]
[Route("api/clientes")]
public sealed class ClientesController(MercuryGoDbContext db) : ControllerBase
{
    // Listado paginado con búsqueda en la base de datos (por Razón Social, CUIT o Teléfono).
    // Cumple con la regla de la cátedra: "buscar y filtrar en la API, nunca en la memoria del teléfono".
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken ct)
    {
        var consulta = db.Clientes.AsNoTracking().Where(c => c.Activo);
        var texto = (busqueda ?? string.Empty).Trim();

        if (texto.Length > 0)
        {
            consulta = consulta.Where(c =>
                c.RazonSocial.Contains(texto) ||
                c.Cuit.Contains(texto) ||
                (c.Telefono != null && c.Telefono.Contains(texto)) ||
                (c.Localidad != null && c.Localidad.Contains(texto))
            );
        }

        var (items, paginaResponse) = await consulta
            .OrderBy(c => c.RazonSocial)
            .Select(c => new ClienteListaResponse(
                c.Id,
                c.RazonSocial,
                c.Cuit,
                c.Localidad,
                c.Direccion,
                c.Telefono,
                c.Email
            ))
            .PaginarAsync(PaginaConsulta.Desde(pagina, tamano), ct);

        return Ok(new { clientes = items, pagina = paginaResponse });
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
                db.Pedidos.Count(p => p.ClienteId == c.Id && p.Activo)
            ))
            .FirstOrDefaultAsync(ct);

        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El comercio/cliente no existe o fue dado de baja." });

        return Ok(new { cliente });
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarClienteRequest request, CancellationToken ct)
    {
        var error = Validar(request);
        if (error is not null) return BadRequest(error);

        var cuitLimpio = NormalizarTexto(request.Cuit) ?? string.Empty;
        var cuitDuplicado = await db.Clientes.AnyAsync(c => c.Cuit == cuitLimpio && c.Activo, ct);
        if (cuitDuplicado)
            return BadRequest(new { codigo = "cliente_cuit_duplicado", mensaje = "Ya existe un cliente o comercio registrado con ese CUIT." });

        var telefonoLimpio = NormalizarTexto(request.Telefono);
        if (!string.IsNullOrEmpty(telefonoLimpio))
        {
            var telefonoDuplicado = await db.Clientes.AnyAsync(c => c.Telefono == telefonoLimpio && c.Activo, ct);
            if (telefonoDuplicado)
                return BadRequest(new { codigo = "cliente_telefono_duplicado", mensaje = "Ya existe un cliente con ese número de teléfono." });
        }

        var cliente = new Cliente
        {
            RazonSocial = request.RazonSocial.Trim(),
            Cuit = cuitLimpio,
            Telefono = telefonoLimpio,
            Email = NormalizarTexto(request.Email),
            Direccion = NormalizarTexto(request.Direccion),
            Localidad = NormalizarTexto(request.Localidad),
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);

        return Ok(new { cliente = Proyectar(cliente) });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] GuardarClienteRequest request, CancellationToken ct)
    {
        var error = Validar(request);
        if (error is not null) return BadRequest(error);

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El cliente no existe o ya fue dado de baja." });

        var cuitLimpio = NormalizarTexto(request.Cuit) ?? string.Empty;
        var cuitDuplicado = await db.Clientes.AnyAsync(c => c.Cuit == cuitLimpio && c.Activo && c.Id != id, ct);
        if (cuitDuplicado)
            return BadRequest(new { codigo = "cliente_cuit_duplicado", mensaje = "Ya existe otro cliente registrado con ese número de CUIT." });

        var telefonoLimpio = NormalizarTexto(request.Telefono);
        if (!string.IsNullOrEmpty(telefonoLimpio))
        {
            var telefonoDuplicado = await db.Clientes.AnyAsync(c => c.Telefono == telefonoLimpio && c.Activo && c.Id != id, ct);
            if (telefonoDuplicado)
                return BadRequest(new { codigo = "cliente_telefono_duplicado", mensaje = "Ya existe otro cliente con ese número de teléfono." });
        }

        cliente.RazonSocial = request.RazonSocial.Trim();
        cliente.Cuit = cuitLimpio;
        cliente.Telefono = telefonoLimpio;
        cliente.Email = NormalizarTexto(request.Email);
        cliente.Direccion = NormalizarTexto(request.Direccion);
        cliente.Localidad = NormalizarTexto(request.Localidad);
        cliente.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { cliente = Proyectar(cliente) });
    }

    // Baja lógica (Soft Delete): un cliente con remitos u órdenes históricas jamás se elimina con DELETE
    // para preservar la integridad referencial y contable.
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id && c.Activo, ct);
        if (cliente is null)
            return NotFound(new { codigo = "cliente_no_encontrado", mensaje = "El cliente no existe o ya fue dado de baja." });

        cliente.Activo = false;
        cliente.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { eliminado = true });
    }

    // La validación vive estrictamente en el servidor:
    private static object? Validar(GuardarClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RazonSocial))
            return new { codigo = "cliente_razon_social_requerida", mensaje = "La razón social o nombre del comercio es obligatorio." };

        if (string.IsNullOrWhiteSpace(request.Cuit))
            return new { codigo = "cliente_cuit_requerido", mensaje = "El número de CUIT es obligatorio para clientes comerciales B2B." };

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
            return new { codigo = "cliente_email_invalido", mensaje = "El correo electrónico no tiene un formato válido." };

        return null;
    }

    private static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }

    private static ClienteListaResponse Proyectar(Cliente c) =>
        new(c.Id, c.RazonSocial, c.Cuit, c.Localidad, c.Direccion, c.Telefono, c.Email);
}

public sealed record GuardarClienteRequest(
    string RazonSocial,
    string Cuit,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Localidad
);

public sealed record ClienteListaResponse(
    long Id,
    string RazonSocial,
    string Cuit,
    string? Localidad,
    string? Direccion,
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
