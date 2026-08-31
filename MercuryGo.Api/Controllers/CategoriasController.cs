using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly MercuryGoDbContext _db;

    public CategoriasController(MercuryGoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var categorias = await _db.Categorias
            .Where(c => c.Activo)
            .Select(c => new
            {
                id = c.Id,
                nombre = c.Nombre,
                descripcion = c.Descripcion
            })
            .ToListAsync(ct);

        return Ok(categorias);
    }
}
