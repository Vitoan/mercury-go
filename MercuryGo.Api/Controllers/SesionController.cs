using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record LogoutRequest(string? RefreshToken, bool Todos = false);

public sealed record UsuarioResponse(
    long Id,
    string Nombre,
    string Email,
    string? Telefono,
    long? RolId,
    string? RolCodigo,
    string? RolNombre
);

public sealed record SesionResponse(
    string Token,
    DateTimeOffset ExpiraEn,
    string RefreshToken,
    DateTimeOffset RefreshExpiraEn,
    UsuarioResponse Usuario
);

[ApiController]
[Route("api/sesion")]
public sealed class SesionController(MercuryGoDbContext db, TokenService tokens) : ControllerBase
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        // Mismo mensaje genérico para "no existe" y "clave incorrecta" por seguridad.
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { codigo = "credenciales_invalidas", mensaje = "Email o contraseña incorrectos." });
        }

        var verificacion = Hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password ?? string.Empty);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { codigo = "credenciales_invalidas", mensaje = "Email o contraseña incorrectos." });
        }

        var sesion = await EmitirSesionAsync(usuario, cancellationToken);
        return Ok(new { sesion });
    }

    // Endpoint público: se invoca cuando el access token expiró.
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var hash = TokenService.HashRefresh(request.RefreshToken ?? string.Empty);
        var guardado = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (guardado is null || guardado.RevocadoEn is not null || guardado.ExpiraEn <= DateTimeOffset.UtcNow)
        {
            return Unauthorized(new { codigo = "refresh_invalido", mensaje = "La sesión expiró. Volvé a iniciar sesión." });
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == guardado.UsuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { codigo = "refresh_invalido", mensaje = "La sesión expiró. Volvé a iniciar sesión." });
        }

        // Rotación estricta de Refresh Token: el presentado queda revocado de inmediato.
        guardado.RevocadoEn = DateTimeOffset.UtcNow;
        var sesion = await EmitirSesionAsync(usuario, cancellationToken);
        return Ok(new { sesion });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        var hash = TokenService.HashRefresh(request.RefreshToken ?? string.Empty);
        var guardado = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (guardado is null) return Ok(new { cerrada = true });

        if (request.Todos)
        {
            var vigentes = await db.RefreshTokens
                .Where(x => x.UsuarioId == guardado.UsuarioId && x.RevocadoEn == null)
                .ToListAsync(cancellationToken);
            vigentes.ForEach(token => token.RevocadoEn = DateTimeOffset.UtcNow);
        }
        else
        {
            guardado.RevocadoEn = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { cerrada = true });
    }

    // [Authorize] sin roles: un usuario sin rol asignado tiene que poder consultar su perfil
    // para que la app sepa que aún no fue habilitado.
    [Authorize]
    [HttpGet("yo")]
    public async Task<IActionResult> Yo(CancellationToken cancellationToken)
    {
        var usuarioId = User.UsuarioId();
        var usuario = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return NotFound(new { codigo = "usuario_no_encontrado", mensaje = "El usuario no existe." });
        }

        var rol = usuario.RolId.HasValue
            ? await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuario.RolId.Value, cancellationToken)
            : null;

        var respuesta = new UsuarioResponse(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.Telefono,
            rol?.Id,
            rol?.Codigo,
            rol?.Nombre
        );

        return Ok(new { usuario = respuesta });
    }

    private async Task<SesionResponse> EmitirSesionAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var rol = usuario.RolId.HasValue
            ? await db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuario.RolId.Value && x.Activo, cancellationToken)
            : null;

        var (token, expiraEn) = tokens.Emitir(usuario, rol);
        var (refresh, refreshHash, refreshExpiraEn) = tokens.EmitirRefresh();

        db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = refreshHash,
            ExpiraEn = refreshExpiraEn,
            CreadoEn = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var usuarioResp = new UsuarioResponse(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.Telefono,
            rol?.Id,
            rol?.Codigo,
            rol?.Nombre
        );

        return new SesionResponse(token, expiraEn, refresh, refreshExpiraEn, usuarioResp);
    }
}
