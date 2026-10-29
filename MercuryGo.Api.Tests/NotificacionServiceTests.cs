using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;
using Xunit;

namespace MercuryGo.Api.Tests;

public sealed class NotificacionServiceTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    private static async Task<(Rol admin, Rol chofer)> SembrarRolesAsync(MercuryGoDbContext db)
    {
        var admin = new Rol { Id = 1, Nombre = "Administrador", Codigo = RolCodigos.Admin };
        var chofer = new Rol { Id = 2, Nombre = "Chofer", Codigo = RolCodigos.Chofer };
        db.Roles.AddRange(admin, chofer);
        await db.SaveChangesAsync();
        return (admin, chofer);
    }

    private static Usuario Usuario(long id, long rolId, bool activo = true) => new()
    {
        Id = id,
        RolId = rolId,
        Nombre = $"Usuario {id}",
        Email = $"usuario{id}@mercurygo.test",
        PasswordHash = "hash",
        Activo = activo
    };

    [Fact]
    public async Task NotificaATodosLosAdminsActivos()
    {
        using var db = CrearContextoEnMemoria("Test_Notificar_Admins");
        var (admin, _) = await SembrarRolesAsync(db);
        db.Usuarios.AddRange(
            Usuario(10, admin.Id),
            Usuario(11, admin.Id),
            Usuario(12, admin.Id, activo: false));
        await db.SaveChangesAsync();

        var servicio = new NotificacionService(db);
        var enviadas = await servicio.NotificarRolAsync(
            RolCodigos.Admin, "Picking incompleto", "Faltan 2 productos", "PEDIDO", 99);
        // El servicio participa de la unidad de trabajo: persiste el llamador.
        await db.SaveChangesAsync();

        Assert.Equal(2, enviadas);

        var notificaciones = await db.Notificaciones.AsNoTracking().ToListAsync();
        Assert.Equal(2, notificaciones.Count);
        Assert.All(notificaciones, n =>
        {
            Assert.Contains(n.UsuarioId, new long[] { 10, 11 });
            Assert.Equal("PEDIDO", n.Tipo);
            Assert.Equal(99, n.ReferenciaId);
        });
    }

    [Fact]
    public async Task NoNotificaAOtrosRoles()
    {
        using var db = CrearContextoEnMemoria("Test_Notificar_OtroRol");
        var (_, chofer) = await SembrarRolesAsync(db);
        db.Usuarios.Add(Usuario(20, chofer.Id));
        await db.SaveChangesAsync();

        var servicio = new NotificacionService(db);
        var enviadas = await servicio.NotificarRolAsync(
            RolCodigos.Admin, "Picking incompleto", "Faltan 2 productos");

        Assert.Equal(0, enviadas);
        Assert.Empty(await db.Notificaciones.AsNoTracking().ToListAsync());
    }
}
