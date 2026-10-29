using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;
using Xunit;

namespace MercuryGo.Api.Tests;

public sealed class ConflictoOfflineServiceTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    private static async Task SembrarAsync(MercuryGoDbContext db)
    {
        db.Roles.AddRange(
            new Rol { Id = 1, Nombre = "Administrador", Codigo = RolCodigos.Admin },
            new Rol { Id = 2, Nombre = "Chofer", Codigo = RolCodigos.Chofer });

        db.Usuarios.AddRange(
            new Usuario { Id = 10, RolId = 1, Nombre = "Ana Admin", Email = "ana@mercurygo.test", PasswordHash = "x" },
            new Usuario { Id = 20, RolId = 2, Nombre = "Carlos Chofer", Email = "carlos@mercurygo.test", PasswordHash = "x" },
            new Usuario { Id = 21, RolId = 2, Nombre = "Diego Chofer", Email = "diego@mercurygo.test", PasswordHash = "x" });

        await db.SaveChangesAsync();
    }

    private static Pedido PedidoCancelado(long choferId = 20) => new()
    {
        Id = 77,
        Numero = "PED-000077",
        Estado = EstadoPedido.Cancelado,
        ChoferId = choferId,
        Activo = true
    };

    [Fact]
    public async Task AvisaAlAdminCuandoSeEntregaUnPedidoCancelado()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_Entrega_Cancelado");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado();
        pedido.Estado = EstadoPedido.Entregado;

        var avisos = await servicio.AvisarEntregaSobreCanceladoAsync(
            pedido, 20, EstadoPedido.Cancelado);
        await db.SaveChangesAsync();

        Assert.Equal(1, avisos);

        var notificacion = await db.Notificaciones.AsNoTracking().SingleAsync();
        Assert.Equal(10, notificacion.UsuarioId);
        Assert.Equal(77, notificacion.ReferenciaId);
        Assert.Contains("cancelado", notificacion.Titulo);
        Assert.Contains("Carlos Chofer", notificacion.Mensaje);
    }

    [Fact]
    public async Task NoAvisaSiElPedidoNoEstabaCancelado()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_Entrega_No_Cancelado");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado();
        pedido.Estado = EstadoPedido.Entregado;

        var avisos = await servicio.AvisarEntregaSobreCanceladoAsync(
            pedido, 20, EstadoPedido.EnPuerta);

        Assert.Equal(0, avisos);
        Assert.Empty(await db.Notificaciones.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AvisaCuandoOtroChoferEntregaUnPedidoReasignado()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_Reasignado");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado(choferId: 21);
        pedido.Estado = EstadoPedido.Entregado;

        var avisos = await servicio.AvisarChoferReasignadoAsync(
            pedido, usuarioId: 20, esChofer: true, EstadoPedido.Entregado);
        await db.SaveChangesAsync();

        Assert.Equal(1, avisos);
        var notificacion = await db.Notificaciones.AsNoTracking().SingleAsync();
        Assert.Contains("Carlos Chofer", notificacion.Mensaje);
        Assert.Contains("Diego Chofer", notificacion.Mensaje);
    }

    [Fact]
    public async Task NoAvisaSiElChoferEsElAsignado()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_Mismo_Chofer");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado(choferId: 20);
        pedido.Estado = EstadoPedido.Entregado;

        var avisos = await servicio.AvisarChoferReasignadoAsync(
            pedido, usuarioId: 20, esChofer: true, EstadoPedido.Entregado);

        Assert.Equal(0, avisos);
        Assert.Empty(await db.Notificaciones.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task NoAvisaReasignacionSiLaAccionNoEsUnCierre()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_EnPuerta");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado(choferId: 21);
        pedido.Estado = EstadoPedido.EnPuerta;

        var avisos = await servicio.AvisarChoferReasignadoAsync(
            pedido, usuarioId: 20, esChofer: true, EstadoPedido.EnPuerta);

        Assert.Equal(0, avisos);
    }

    [Fact]
    public async Task AvisaElPickingSoloSiLlegoSinRedYLaOrdenEstaCancelada()
    {
        using var db = CrearContextoEnMemoria("Test_Conflicto_Picking");
        await SembrarAsync(db);
        var servicio = new ConflictoOfflineService(db, new NotificacionService(db));

        var pedido = PedidoCancelado();
        pedido.Estado = EstadoPedido.Cancelado;

        var sinRed = await servicio.AvisarPickingSobreCanceladoAsync(
            pedido, usuarioId: 20, conFaltantes: true, esOffline: true);

        var enLinea = await servicio.AvisarPickingSobreCanceladoAsync(
            pedido, usuarioId: 20, conFaltantes: true, esOffline: false);

        await db.SaveChangesAsync();

        Assert.Equal(1, sinRed);
        Assert.Equal(0, enLinea);
        var notificacion = await db.Notificaciones.AsNoTracking().SingleAsync();
        Assert.Contains("faltantes", notificacion.Mensaje);
    }
}
