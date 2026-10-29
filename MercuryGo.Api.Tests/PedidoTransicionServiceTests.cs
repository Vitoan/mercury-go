using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;
using Xunit;

namespace MercuryGo.Api.Tests;

public class PedidoTransicionServiceTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public async Task Admin_PuedeConfirmarPedido_DesdePendiente()
    {
        // Arrange
        using var db = CrearContextoEnMemoria("Test_Confirmar_Admin");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 1,
            Numero = "PED-001",
            Estado = EstadoPedido.Pendiente,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 99,
            RolCodigo: RolCodigos.Admin,
            EsSincronizacionOffline: false
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Confirmado, contexto);

        // Assert
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoPedido.Pendiente, resultado.EstadoAnterior);
        Assert.Equal(EstadoPedido.Confirmado, resultado.NuevoEstado);
        Assert.Equal(EstadoPedido.Confirmado, pedido.Estado);
        Assert.Equal(99, pedido.ConfirmadoPorId);
        Assert.NotNull(pedido.ConfirmadoEn);
    }

    [Fact]
    public async Task Operario_NoPuedeConfirmarPedido_DesdePendiente()
    {
        // Arrange
        using var db = CrearContextoEnMemoria("Test_Confirmar_Operario");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 2,
            Numero = "PED-002",
            Estado = EstadoPedido.Pendiente,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 10,
            RolCodigo: RolCodigos.Operario,
            EsSincronizacionOffline: false
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Confirmado, contexto);

        // Assert
        Assert.False(resultado.Exito);
        Assert.Equal("rol_no_autorizado", resultado.CodigoError);
        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
    }

    [Fact]
    public async Task Chofer_Offline_EntregaPedidoCancelado_GanaChoferPorExcepcionOffline()
    {
        // Arrange (Decisión 10: Chofer entrega offline mientras el admin canceló)
        using var db = CrearContextoEnMemoria("Test_Offline_GanaChofer");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 3,
            Numero = "PED-003",
            Estado = EstadoPedido.Cancelado,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 5,
            RolCodigo: RolCodigos.Chofer,
            Latitud: -31.42008,
            Longitud: -64.18877,
            EsSincronizacionOffline: true
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Entregado, contexto);

        // Assert
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
        Assert.NotNull(pedido.EntregadoEn);
        Assert.Equal(-31.42008, pedido.LatitudEntrega);
        Assert.Equal(-64.18877, pedido.LongitudEntrega);
        Assert.Contains("[Aviso Sincronización Offline", pedido.Observaciones ?? "");
    }

    [Fact]
    public async Task Transicion_AlMismoEstado_EsIdempotente()
    {
        // Arrange
        using var db = CrearContextoEnMemoria("Test_Idempotencia_Estado");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 4,
            Numero = "PED-004",
            Estado = EstadoPedido.Entregado,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 5,
            RolCodigo: RolCodigos.Chofer
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Entregado, contexto);

        // Assert
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoPedido.Entregado, resultado.EstadoAnterior);
        Assert.Equal(EstadoPedido.Entregado, resultado.NuevoEstado);
    }

    [Fact]
    public async Task Admin_PuedeReintentarEntrega_DesdeNoEntregado()
    {
        // Arrange (CU-21: NoEntregado -> Despachado)
        using var db = CrearContextoEnMemoria("Test_Reintento_Entrega");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 5,
            Numero = "PED-005",
            Estado = EstadoPedido.NoEntregado,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 1,
            RolCodigo: RolCodigos.Admin
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Despachado, contexto);

        // Assert
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoPedido.Despachado, pedido.Estado);
    }

    [Fact]
    public async Task Chofer_PuedeMarcarNoEntregado_DesdeDespachado()
    {
        // Arrange (P1-8: el chofer puede fallar la entrega sin pasar por EnPuerta)
        using var db = CrearContextoEnMemoria("Test_NoEntregado_DesdeDespachado");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 6,
            Numero = "PED-006",
            Estado = EstadoPedido.Despachado,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 5,
            RolCodigo: RolCodigos.Chofer
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.NoEntregado, contexto);

        // Assert
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoPedido.NoEntregado, pedido.Estado);
    }

    [Fact]
    public async Task Chofer_NoPuedeReintentarEntrega_DesdeNoEntregado()
    {
        // Arrange (CU-21: el reintento lo autoriza el Admin)
        using var db = CrearContextoEnMemoria("Test_Reintento_Chofer_Rechazado");
        var servicio = new PedidoTransicionService(db);
        var pedido = new Pedido
        {
            Id = 7,
            Numero = "PED-007",
            Estado = EstadoPedido.NoEntregado,
            FechaPedido = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var contexto = new ContextoTransicion(
            UsuarioId: 5,
            RolCodigo: RolCodigos.Chofer
        );

        // Act
        var resultado = await servicio.TransicionarAsync(pedido, EstadoPedido.Despachado, contexto);

        // Assert
        Assert.False(resultado.Exito);
        Assert.Equal("rol_no_autorizado", resultado.CodigoError);
        Assert.Equal(EstadoPedido.NoEntregado, pedido.Estado);
    }
}
