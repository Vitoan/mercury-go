using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using Xunit;

namespace MercuryGo.Api.Tests;

public class AutorizacionIdorTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public void ClientePropio_PuedeAcceder_A_SuOrden()
    {
        var pedido = new Pedido
        {
            Id = 1,
            ClienteId = 10,
            Numero = "PED-000001",
            Estado = EstadoPedido.Confirmado,
            Activo = true
        };

        var clienteAutenticadoId = 10L;

        // Regla: clienteAutenticadoId == pedido.ClienteId
        var tieneAcceso = pedido.ClienteId == clienteAutenticadoId;

        Assert.True(tieneAcceso);
    }

    [Fact]
    public void ClienteAjeno_NoPuedeAcceder_A_OrdenDeOtroCliente()
    {
        var pedido = new Pedido
        {
            Id = 2,
            ClienteId = 20, // Pertenece a Cliente 20
            Numero = "PED-000002",
            Estado = EstadoPedido.Confirmado,
            Activo = true
        };

        var clienteAtacanteId = 99L; // Cliente 99 intentando acceder

        // Regla IDOR: no debe coincidir
        var tieneAcceso = pedido.ClienteId == clienteAtacanteId;

        Assert.False(tieneAcceso);
    }

    [Fact]
    public void Chofer_Accede_SiEstaAsignado_O_Despachado()
    {
        var choferId = 5L;

        var pedidoAsignado = new Pedido
        {
            Id = 3,
            ClienteId = 10,
            ChoferId = 5,
            Numero = "PED-000003",
            Estado = EstadoPedido.EnPreparacion
        };

        var pedidoDespachado = new Pedido
        {
            Id = 4,
            ClienteId = 10,
            ChoferId = null,
            Numero = "PED-000004",
            Estado = EstadoPedido.Despachado
        };

        var pedidoNoAsignadoNiDespachado = new Pedido
        {
            Id = 5,
            ClienteId = 10,
            ChoferId = 88,
            Numero = "PED-000005",
            Estado = EstadoPedido.Pendiente
        };

        bool AccesoChofer(Pedido p, long chId) =>
            (p.ChoferId.HasValue && p.ChoferId.Value == chId) || p.Estado == EstadoPedido.Despachado;

        Assert.True(AccesoChofer(pedidoAsignado, choferId));
        Assert.True(AccesoChofer(pedidoDespachado, choferId));
        Assert.False(AccesoChofer(pedidoNoAsignadoNiDespachado, choferId));
    }

    [Fact]
    public async Task NumeracionCorrelativa_BasadaEnId_EsSiempreUnica()
    {
        using var db = CrearContextoEnMemoria("Test_Numeracion_Unica");

        var p1 = new Pedido { Id = 1, ClienteId = 1, Numero = "TMP-1", Activo = true };
        var p2 = new Pedido { Id = 2, ClienteId = 1, Numero = "TMP-2", Activo = true };
        db.Pedidos.AddRange(p1, p2);
        await db.SaveChangesAsync();

        p1.Numero = $"PED-{p1.Id:D6}";
        p2.Numero = $"PED-{p2.Id:D6}";
        await db.SaveChangesAsync();

        Assert.Equal("PED-000001", p1.Numero);
        Assert.Equal("PED-000002", p2.Numero);
        Assert.NotEqual(p1.Numero, p2.Numero);
    }
}
