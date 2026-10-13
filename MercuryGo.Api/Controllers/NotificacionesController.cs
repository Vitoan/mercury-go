using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
public sealed class NotificacionesController(MercuryGoDbContext db) : ControllerBase
{
    // Obtener notificaciones del usuario autenticado (con conteo de no leídas)
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 20, CancellationToken ct = default)
    {
        var usuarioId = User.UsuarioId();
        if (usuarioId <= 0)
        {
            return Unauthorized(new { codigo = "usuario_invalido", mensaje = "Token sin identificador de usuario válido." });
        }

        var noLeidas = await db.Notificaciones
            .CountAsync(n => n.UsuarioId == usuarioId && !n.Leida, ct);

        var items = await db.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CreadoEn)
            .Take(limite)
            .Select(n => new NotificacionResponse(
                n.Id,
                n.Titulo,
                n.Mensaje,
                n.Tipo,
                n.Leida,
                n.CreadoEn,
                n.ReferenciaId
            ))
            .ToListAsync(ct);

        return Ok(new
        {
            no_leidas = noLeidas,
            items
        });
    }

    // Marcar una notificación individual como leída
    [HttpPut("{id:long}/leer")]
    public async Task<IActionResult> MarcarLeida(long id, CancellationToken ct)
    {
        var usuarioId = User.UsuarioId();
        var notif = await db.Notificaciones
            .FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId, ct);

        if (notif is null)
        {
            return NotFound(new { codigo = "notificacion_no_encontrada", mensaje = "La notificación no existe o no te pertenece." });
        }

        notif.Leida = true;
        notif.ActualizadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { leida = true });
    }

    // Marcar todas las notificaciones del usuario como leídas
    [HttpPut("marcar-todas-leidas")]
    public async Task<IActionResult> MarcarTodasLeidas(CancellationToken ct)
    {
        var usuarioId = User.UsuarioId();
        var pendientes = await db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leida)
            .ToListAsync(ct);

        foreach (var p in pendientes)
        {
            p.Leida = true;
            p.ActualizadoEn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { marcadas = pendientes.Count });
    }
}

public sealed record NotificacionResponse(
    long Id,
    string Titulo,
    string Mensaje,
    string Tipo,
    bool Leida,
    DateTime Fecha,
    long? ReferenciaId
);
