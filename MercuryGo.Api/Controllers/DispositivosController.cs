using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Controllers;

// Gestión de tokens de notificación push para dispositivos móviles autenticados.
// La app informa el token FCM del dispositivo donde se encuentra abierta la sesión.
// La identidad se extrae estrictamente del JWT autenticado.
[ApiController]
[Authorize]
[Route("api/dispositivos")]
public sealed class DispositivosController(MercuryGoDbContext db) : ControllerBase
{
    // Alta o reasignación: si el token ya existía en la base (ej. el mismo teléfono físico
    // usado por otro usuario previamente), se reasigna al usuario actual para evitar cruce de avisos.
    [HttpPut]
    public async Task<IActionResult> Registrar([FromBody] RegistrarDispositivoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { codigo = "token_requerido", mensaje = "Falta el token del dispositivo." });
        }

        var usuarioId = User.UsuarioId();
        if (usuarioId <= 0)
        {
            return Unauthorized(new { codigo = "no_autorizado", mensaje = "Sesión no válida." });
        }

        var dispositivo = await db.DispositivosPush.FirstOrDefaultAsync(x => x.Token == request.Token, cancellationToken);
        if (dispositivo is null)
        {
            db.DispositivosPush.Add(new DispositivoPush
            {
                UsuarioId = usuarioId,
                Token = request.Token
            });
        }
        else
        {
            dispositivo.UsuarioId = usuarioId;
            dispositivo.ActualizadoEn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { registrado = true });
    }

    // Baja del token push al cerrar sesión (DEF-30) para evitar que notificaciones
    // posteriores del usuario lleguen a este dispositivo si otro usuario inicia sesión.
    [HttpDelete]
    public async Task<IActionResult> Desregistrar([FromBody] RegistrarDispositivoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { codigo = "token_requerido", mensaje = "Falta el token del dispositivo." });
        }

        var dispositivo = await db.DispositivosPush.FirstOrDefaultAsync(x => x.Token == request.Token, cancellationToken);
        if (dispositivo is not null)
        {
            db.DispositivosPush.Remove(dispositivo);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { desregistrado = true });
    }
}

public sealed record RegistrarDispositivoRequest(string Token);
