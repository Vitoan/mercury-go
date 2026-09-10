using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize(Roles = RolCodigos.Admin)]
[Route("api/productos")]
public sealed class ProductosController(MercuryGoDbContext db) : ControllerBase
{
    // Vitrina no paginada: muestra el catálogo completo agrupado por categoría para inicio/resumen.
    [AllowAnonymous]
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen(CancellationToken cancellationToken)
    {
        var total = await db.Productos.CountAsync(x => x.Activo, cancellationToken);
        var disponibles = await db.Productos.CountAsync(x => x.Activo && x.Disponible, cancellationToken);
        var resumen = new ProductosResumenResponse(total, disponibles, total - disponibles);

        var categoriasBase = await db.Categorias.AsNoTracking()
            .Where(x => x.Activo)
            .Select(categoria => new
            {
                categoria.Id,
                categoria.Nombre,
                categoria.Descripcion,
                Cantidad = db.Productos.Count(producto => producto.CategoriaId == categoria.Id && producto.Activo)
            })
            .Where(x => x.Cantidad > 0)
            .OrderBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var categorias = categoriasBase
            .Select(x => new CategoriaProductoResponse(x.Id, x.Nombre, x.Descripcion, x.Cantidad))
            .ToList();

        var productos = await (
            from producto in db.Productos.AsNoTracking()
            join categoria in db.Categorias.AsNoTracking() on producto.CategoriaId equals categoria.Id
            where producto.Activo
            orderby categoria.Nombre, producto.Nombre
            select new ProductoResumenResponse(
                producto.Id,
                producto.Nombre,
                producto.Descripcion,
                producto.Precio,
                producto.ImagenUrl,
                producto.Disponible,
                categoria.Id,
                categoria.Nombre
            )
        ).ToListAsync(cancellationToken);

        return Ok(new { resumen, categorias, productos });
    }

    // Catálogo paginado con filtros en servidor: busca por texto, categoría y disponibilidad.
    [AllowAnonymous]
    [HttpGet("listado")]
    public async Task<IActionResult> Listado(
        [FromQuery] string? busqueda,
        [FromQuery] long? categoria_id,
        [FromQuery] bool? disponible,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken ct)
    {
        var consulta = db.Productos.AsNoTracking().Where(p => p.Activo);

        var texto = (busqueda ?? string.Empty).Trim();
        if (texto.Length > 0)
        {
            consulta = consulta.Where(p => p.Nombre.Contains(texto) || (p.Descripcion != null && p.Descripcion.Contains(texto)));
        }

        if (categoria_id.HasValue && categoria_id.Value > 0)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoria_id.Value);
        }

        if (disponible.HasValue)
        {
            consulta = consulta.Where(p => p.Disponible == disponible.Value);
        }

        var (items, paginaResponse) = await (
            from p in consulta
            join c in db.Categorias.AsNoTracking() on p.CategoriaId equals c.Id
            orderby p.Nombre
            select new ProductoResumenResponse(
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.ImagenUrl,
                p.Disponible,
                c.Id,
                c.Nombre
            )
        ).PaginarAsync(PaginaConsulta.Desde(pagina, tamano), ct);

        return Ok(new { productos = items, pagina = paginaResponse });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalle(long id, CancellationToken cancellationToken)
    {
        var producto = await (
            from p in db.Productos.AsNoTracking()
            join categoria in db.Categorias.AsNoTracking() on p.CategoriaId equals categoria.Id
            where p.Id == id && p.Activo
            select new ProductoResumenResponse(
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.ImagenUrl,
                p.Disponible,
                categoria.Id,
                categoria.Nombre
            )
        ).FirstOrDefaultAsync(cancellationToken);

        if (producto is null)
            return NotFound(new { codigo = "producto_no_encontrado", mensaje = "El producto no existe o fue dado de baja." });

        return Ok(new { producto });
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarProductoRequest request, CancellationToken ct)
    {
        var error = await ValidarAsync(request, ct);
        if (error is not null) return BadRequest(error);

        var producto = new Producto
        {
            CategoriaId = request.CategoriaId,
            Nombre = request.Nombre.Trim(),
            Descripcion = NormalizarTexto(request.Descripcion),
            Precio = request.Precio,
            ImagenUrl = NormalizarTexto(request.ImagenUrl),
            Disponible = request.Disponible,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };

        db.Productos.Add(producto);
        await db.SaveChangesAsync(ct);

        return Ok(new { producto = await ObtenerProyectadoAsync(producto.Id, ct) });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] GuardarProductoRequest request, CancellationToken ct)
    {
        var error = await ValidarAsync(request, ct);
        if (error is not null) return BadRequest(error);

        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (producto is null)
            return NotFound(new { codigo = "producto_no_encontrado", mensaje = "El producto no existe o fue dado de baja." });

        producto.CategoriaId = request.CategoriaId;
        producto.Nombre = request.Nombre.Trim();
        producto.Descripcion = NormalizarTexto(request.Descripcion);
        producto.Precio = request.Precio;
        producto.ImagenUrl = NormalizarTexto(request.ImagenUrl);
        producto.Disponible = request.Disponible;
        producto.ActualizadoEn = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { producto = await ObtenerProyectadoAsync(producto.Id, ct) });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id && p.Activo, ct);
        if (producto is null)
            return NotFound(new { codigo = "producto_no_encontrado", mensaje = "El producto no existe o ya fue dado de baja." });

        producto.Activo = false;
        producto.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { eliminado = true });
    }

    private async Task<object?> ValidarAsync(GuardarProductoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return new { codigo = "producto_nombre_requerido", mensaje = "El nombre del producto o mercadería es obligatorio." };

        if (request.Precio <= 0)
            return new { codigo = "producto_precio_invalido", mensaje = "El precio unitario debe ser mayor a cero." };

        var categoriaExiste = await db.Categorias.AnyAsync(c => c.Id == request.CategoriaId && c.Activo, ct);
        if (!categoriaExiste)
            return new { codigo = "categoria_invalida", mensaje = "La categoría especificada no existe o está inactiva." };

        return null;
    }

    private async Task<ProductoResumenResponse?> ObtenerProyectadoAsync(long id, CancellationToken ct)
    {
        return await (
            from p in db.Productos.AsNoTracking()
            join c in db.Categorias.AsNoTracking() on p.CategoriaId equals c.Id
            where p.Id == id
            select new ProductoResumenResponse(
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Precio,
                p.ImagenUrl,
                p.Disponible,
                c.Id,
                c.Nombre
            )
        ).FirstOrDefaultAsync(ct);
    }

    private static string? NormalizarTexto(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}

public sealed record GuardarProductoRequest(
    long CategoriaId,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    string? ImagenUrl,
    bool Disponible
);

public sealed record ProductosResumenResponse(int Total, int Disponibles, int NoDisponibles);
public sealed record CategoriaProductoResponse(long Id, string Nombre, string? Descripcion, int Cantidad);
public sealed record ProductoResumenResponse(
    long Id,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    string? ImagenUrl,
    bool Disponible,
    long CategoriaId,
    string CategoriaNombre
);
