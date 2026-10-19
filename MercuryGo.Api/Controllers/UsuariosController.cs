using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

public sealed record CrearUsuarioRequest(
    string? Nombre,
    string? Email,
    string? Password,
    string? Telefono,
    long? RolId
);

public sealed record CambiarRolRequest(long? RolId);

public sealed record RolDto(long Id, string Codigo, string Nombre, string? Descripcion);

[ApiController]
[Authorize(Roles = RolCodigos.Admin)]
[Route("api/usuarios")]
public sealed class UsuariosController(MercuryGoDbContext db) : ControllerBase
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    // Listado paginado de usuarios para el panel de administración
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] int? pagina,
        [FromQuery] int? tamano,
        CancellationToken cancellationToken)
    {
        var consulta = db.Usuarios.AsNoTracking().Where(x => x.Activo);
        var texto = (busqueda ?? string.Empty).Trim();
        if (texto.Length > 0)
        {
            consulta = consulta.Where(x => x.Nombre.Contains(texto) || x.Email.Contains(texto) || (x.Telefono != null && x.Telefono.Contains(texto)));
        }

        var p = Math.Max(1, pagina ?? 1);
        var t = Math.Clamp(tamano ?? 10, 1, 50);

        var total = await consulta.CountAsync(cancellationToken);
        var usuarios = await (
            from u in consulta
            join r in db.Roles.AsNoTracking() on u.RolId equals r.Id into roles
            from rol in roles.DefaultIfEmpty()
            orderby u.Nombre
            select new UsuarioResponse(
                u.Id,
                u.Nombre,
                u.Email,
                u.Telefono,
                rol != null ? rol.Id : null,
                rol != null ? rol.Codigo : null,
                rol != null ? rol.Nombre : null
            )
        )
        .Skip((p - 1) * t)
        .Take(t)
        .ToListAsync(cancellationToken);

        var rolesDisponibles = await db.Roles.AsNoTracking()
            .Where(r => r.Activo)
            .OrderBy(r => r.Id)
            .Select(r => new RolDto(r.Id, r.Codigo, r.Nombre, r.Descripcion))
            .ToListAsync(cancellationToken);

        var hayMas = (p * t) < total;
        var paginaInfo = new
        {
            pagina = p,
            tamano = t,
            total,
            hay_mas = hayMas
        };

        return Ok(new { usuarios, pagina = paginaInfo, roles = rolesDisponibles });
    }

    // Alta de usuario: permite crearlo con o sin rol asignado
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { codigo = "usuario_nombre_requerido", mensaje = "El nombre del usuario es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { codigo = "usuario_email_invalido", mensaje = "El email no tiene un formato válido." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { codigo = "password_corta", mensaje = "La contraseña debe tener al menos 8 caracteres." });

        var email = request.Email.Trim().ToLowerInvariant();
        var duplicado = await db.Usuarios.AnyAsync(x => x.Email == email, cancellationToken);
        if (duplicado)
            return BadRequest(new { codigo = "usuario_duplicado", mensaje = "Ya existe un usuario registrado con ese email." });

        if (request.RolId.HasValue)
        {
            var existeRol = await db.Roles.AnyAsync(r => r.Id == request.RolId.Value && r.Activo, cancellationToken);
            if (!existeRol)
                return BadRequest(new { codigo = "rol_no_encontrado", mensaje = "El rol especificado no existe o no está activo." });
        }

        var usuario = new Usuario
        {
            Nombre = request.Nombre.Trim(),
            Email = email,
            Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
            RolId = request.RolId,
            Activo = true,
            CreadoEn = DateTime.UtcNow
        };
        usuario.PasswordHash = Hasher.HashPassword(usuario, request.Password);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);

        var rol = usuario.RolId.HasValue
            ? await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == usuario.RolId.Value, cancellationToken)
            : null;

        var respuesta = new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Email, usuario.Telefono, rol?.Id, rol?.Codigo, rol?.Nombre);
        return Ok(new { usuario = respuesta });
    }

    // Modificación / Asignación de rol
    [HttpPut("{id:long}/rol")]
    public async Task<IActionResult> CambiarRol(long id, [FromBody] CambiarRolRequest request, CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.Activo, cancellationToken);
        if (usuario is null)
            return NotFound(new { codigo = "usuario_no_encontrado", mensaje = "El usuario no existe o fue dado de baja." });

        if (request.RolId.HasValue)
        {
            var existeRol = await db.Roles.AnyAsync(r => r.Id == request.RolId.Value && r.Activo, cancellationToken);
            if (!existeRol)
                return BadRequest(new { codigo = "rol_no_encontrado", mensaje = "El rol especificado no existe." });
        }

        usuario.RolId = request.RolId;
        usuario.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var rol = usuario.RolId.HasValue
            ? await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == usuario.RolId.Value, cancellationToken)
            : null;

        var respuesta = new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Email, usuario.Telefono, rol?.Id, rol?.Codigo, rol?.Nombre);
        return Ok(new { usuario = respuesta });
    }

    // Baja lógica de usuario y revocación inmediata de todas sus sesiones activas
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
    {
        if (id == User.UsuarioId())
            return BadRequest(new { codigo = "autobaja", mensaje = "No podés darte de baja a vos mismo." });

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.Activo, cancellationToken);
        if (usuario is null)
            return NotFound(new { codigo = "usuario_no_encontrado", mensaje = "El usuario no existe o ya fue dado de baja." });

        usuario.Activo = false;
        usuario.ActualizadoEn = DateTime.UtcNow;

        // Revocar todos los refresh tokens activos del usuario
        var tokensActivos = await db.RefreshTokens
            .Where(r => r.UsuarioId == id && r.RevocadoEn == null)
            .ToListAsync(cancellationToken);
        tokensActivos.ForEach(t => t.RevocadoEn = DateTimeOffset.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { eliminado = true });
    }
}
