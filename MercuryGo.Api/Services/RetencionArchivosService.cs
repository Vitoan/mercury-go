using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;

namespace MercuryGo.Api.Services;

public sealed class RetencionArchivosService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<RetencionArchivosService> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloEjecucion = TimeSpan.FromHours(24);
    private static readonly TimeSpan AntiguedadMaxima = TimeSpan.FromDays(60); // 2 meses (Decisión 21)

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Servicio de retención de archivos de entrega iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgarArchivosAntiguosAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al ejecutar purga de archivos de entrega antiguos.");
            }

            await Task.Delay(IntervaloEjecucion, stoppingToken);
        }
    }

    private async Task PurgarArchivosAntiguosAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MercuryGoDbContext>();

        var limite = DateTime.UtcNow.Subtract(AntiguedadMaxima);

        // Buscar pedidos entregados hace más de 2 meses que aún tengan fotos o firmas
        var pedidos = await db.Pedidos
            .Where(p => p.EntregadoEn.HasValue && p.EntregadoEn.Value < limite &&
                       (p.EntregaFotoUrl != null || p.EntregaFirmaUrl != null))
            .ToListAsync(ct);

        if (pedidos.Count == 0)
            return;

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        foreach (var p in pedidos)
        {
            EliminarArchivoFisico(webRoot, p.EntregaFotoUrl);
            EliminarArchivoFisico(webRoot, p.EntregaFirmaUrl);

            p.EntregaFotoUrl = null;
            p.EntregaFirmaUrl = null;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Purga de archivos completada: {Cantidad} pedidos procesados.", pedidos.Count);
    }

    private void EliminarArchivoFisico(string webRoot, string? urlRelativa)
    {
        if (string.IsNullOrWhiteSpace(urlRelativa))
            return;

        try
        {
            var rutaNormalizada = urlRelativa.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var rutaFisica = Path.Combine(webRoot, rutaNormalizada);
            if (File.Exists(rutaFisica))
            {
                File.Delete(rutaFisica);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo eliminar el archivo físico {Url}", urlRelativa);
        }
    }
}
