using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Route("api/productos")]
public sealed class ProductosController(MercuryGoDbContext db) : ControllerBase
{
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
        {
            return NotFound(new { codigo = "producto_no_encontrado", mensaje = "El producto no existe o fue dado de baja." });
        }

        return Ok(new { producto });
    }
}

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
