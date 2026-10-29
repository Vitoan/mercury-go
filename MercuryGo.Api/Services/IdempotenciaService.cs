using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Services;

public sealed class IdempotenciaService(MercuryGoDbContext db)
{
    public async Task<OperacionProcesada?> ObtenerOperacionAsync(string? idempotencyKey, string tipo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var key = idempotencyKey.Trim();
        return await db.OperacionesProcesadas
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == key && o.Tipo == tipo, ct);
    }

    public async Task RegistrarOperacionAsync(
        string? idempotencyKey,
        string tipo,
        long? usuarioId,
        long? pedidoId,
        string? resultadoJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return;

        var key = idempotencyKey.Trim();
        var existe = await db.OperacionesProcesadas.AnyAsync(o => o.Id == key && o.Tipo == tipo, ct);
        if (existe)
            return;

        db.OperacionesProcesadas.Add(new OperacionProcesada
        {
            Id = key,
            Tipo = tipo,
            UsuarioId = usuarioId,
            PedidoId = pedidoId,
            ResultadoJson = resultadoJson,
            ProcesadaEn = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Absorber colisión por concurrencia si otra transacción insertó la misma clave simultáneamente
            db.ChangeTracker.Clear();
        }
    }
}
