using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Push;

// Interceptor SaveChangesInterceptor:
// Despacha automáticamente hacia FCM cualquier notificación creada en el sistema.
// Se ejecuta DESPUÉS de guardar (SavedChangesAsync) para asegurar que la acción de negocio
// se consolidó en la base de datos antes de notificar al dispositivo móvil.
// Si FCM falla, no revierte ni interrumpe la operación principal: el aviso queda seguro en la bandeja in-app.
public sealed class PushAlGuardar(EnvioPush envio, ILogger<PushAlGuardar> logger) : SaveChangesInterceptor
{
    private List<Notificacion> pendientes = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            pendientes = eventData.Context.ChangeTracker.Entries<Notificacion>()
                .Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity)
                .ToList();
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var nuevas = pendientes;
        pendientes = [];
        if (nuevas.Count > 0 && eventData.Context is MercuryGoDbContext db)
        {
            try
            {
                await envio.EnviarAsync(db, nuevas, cancellationToken);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "No se pudo enviar la push a través de FCM; el aviso quedó registrado en la bandeja.");
            }
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
