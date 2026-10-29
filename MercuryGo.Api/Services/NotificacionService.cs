using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

// Avisos in-app dirigidos a un rol completo (por ejemplo, todos los Admin cuando
// el picking queda incompleto). No envía push: solo deja la notificación en la
// bandeja del usuario, que es la que consulta NotificacionesController.
// Participa de la unidad de trabajo del request: el llamador hace SaveChanges.
public sealed class NotificacionService(MercuryGoDbContext db)
{
    public async Task<int> NotificarRolAsync(
        string rolCodigo,
        string titulo,
        string mensaje,
        string tipo = "INFO",
        long? referenciaId = null,
        CancellationToken ct = default)
    {
        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.Codigo == rolCodigo)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roles.Count == 0)
            return 0;

        var usuarios = await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && u.RolId.HasValue && roles.Contains(u.RolId.Value))
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (usuarios.Count == 0)
            return 0;

        var ahora = DateTime.UtcNow;
        foreach (var usuarioId in usuarios)
        {
            db.Notificaciones.Add(new Notificacion
            {
                UsuarioId = usuarioId,
                Titulo = titulo,
                Mensaje = mensaje,
                Tipo = tipo,
                ReferenciaId = referenciaId,
                CreadoEn = ahora
            });
        }

        return usuarios.Count;
    }
}
