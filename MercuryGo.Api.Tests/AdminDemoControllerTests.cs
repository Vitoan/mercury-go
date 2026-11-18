using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Controllers;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using Xunit;

namespace MercuryGo.Api.Tests;

public class AdminDemoControllerTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public void Controller_ExigeRolAdmin()
    {
        var authorize = typeof(AdminDemoController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(RolCodigos.Admin, authorize!.Roles);
    }

    [Fact]
    public async Task ResetDemo_RestauraStockYPedidosDeReserva()
    {
        await using var db = CrearContextoEnMemoria(nameof(ResetDemo_RestauraStockYPedidosDeReserva));

        db.Productos.AddRange(
            new Producto { Id = 1, CategoriaId = 1, Nombre = "Aceite", Precio = 2400m, StockActual = 0, Disponible = false, Activo = false },
            new Producto { Id = 2, CategoriaId = 2, Nombre = "Harina", Precio = 950m, StockActual = 5, Disponible = true, Activo = true }
        );

        db.Pedidos.AddRange(
            new Pedido { Id = 2, ClienteId = 2, Numero = "PED-000002", Estado = EstadoPedido.Entregado, ChoferId = null, Activo = false },
            new Pedido { Id = 3, ClienteId = 1, Numero = "PED-000003", Estado = EstadoPedido.Despachado, ChoferId = 3, Activo = true },
            new Pedido { Id = 5, ClienteId = 2, Numero = "PED-000005", Estado = EstadoPedido.Cancelado, ChoferId = 3, Activo = true }
        );

        await db.SaveChangesAsync();

        var controller = new AdminDemoController(db);
        var result = await controller.ResetDemo(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        var p1 = await db.Productos.FindAsync(1L);
        var p2 = await db.Productos.FindAsync(2L);
        Assert.Equal(150, p1!.StockActual);
        Assert.True(p1.Disponible);
        Assert.True(p1.Activo);
        Assert.Equal(150, p2!.StockActual);

        var ped2 = await db.Pedidos.FindAsync(2L);
        var ped3 = await db.Pedidos.FindAsync(3L);
        var ped5 = await db.Pedidos.FindAsync(5L);

        Assert.Equal(EstadoPedido.Despachado, ped2!.Estado);
        Assert.Equal(3L, ped2.ChoferId);
        Assert.True(ped2.Activo);

        Assert.Equal(EstadoPedido.Confirmado, ped3!.Estado);
        Assert.Null(ped3.ChoferId);

        Assert.Equal(EstadoPedido.Pendiente, ped5!.Estado);
        Assert.Equal(1L, ped5.ClienteId);
        Assert.Null(ped5.ChoferId);
    }
}
