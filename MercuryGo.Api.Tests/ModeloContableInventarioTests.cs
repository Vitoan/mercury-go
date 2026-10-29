using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using Xunit;

namespace MercuryGo.Api.Tests;

public class ModeloContableInventarioTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public async Task Producto_StockDescuento_ReduceStockActual()
    {
        using var db = CrearContextoEnMemoria("Test_Stock_Descuento");
        var producto = new Producto
        {
            Id = 100,
            Nombre = "Harina 000 1kg",
            Precio = 1200m,
            StockActual = 50,
            StockMinimo = 10,
            Disponible = true,
            Activo = true
        };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();

        // Simular consumo de 15 unidades
        var cantidadPedida = 15;
        Assert.True(producto.StockActual >= cantidadPedida);
        producto.StockActual -= cantidadPedida;
        await db.SaveChangesAsync();

        var actualizado = await db.Productos.FirstAsync(p => p.Id == 100);
        Assert.Equal(35, actualizado.StockActual);
    }

    [Fact]
    public async Task Producto_StockInsuficiente_DetectaQuiebre()
    {
        using var db = CrearContextoEnMemoria("Test_Stock_Insuficiente");
        var producto = new Producto
        {
            Id = 101,
            Nombre = "Aceite Girasol 1.5L",
            Precio = 2500m,
            StockActual = 5,
            StockMinimo = 10,
            Disponible = true,
            Activo = true
        };
        db.Productos.Add(producto);
        await db.SaveChangesAsync();

        var cantidadSolicitada = 10;
        var alcanzaStock = producto.StockActual >= cantidadSolicitada;

        Assert.False(alcanzaStock);
    }

    [Fact]
    public async Task PagoCuentaCorriente_Aprobado_ReduceSaldoDeudor()
    {
        using var db = CrearContextoEnMemoria("Test_PagoCC_Aprobado");
        var cliente = new Cliente
        {
            Id = 200,
            RazonSocial = "Almacén Central",
            Cuit = "30-11223344-5",
            Activo = true
        };
        db.Clientes.Add(cliente);

        // Facturado: 100.000
        var pedido = new Pedido
        {
            Id = 300,
            ClienteId = 200,
            Numero = "PED-000300",
            Estado = EstadoPedido.Entregado,
            Activo = true
        };
        db.Pedidos.Add(pedido);
        db.DetallesPedido.Add(new DetallePedido
        {
            PedidoId = 300,
            ProductoId = 1,
            Cantidad = 10,
            PrecioUnitario = 10000m
        });

        // Pago aprobado: 40.000
        var pagoAprobado = new PagoCuentaCorriente
        {
            Id = 1,
            ClienteId = 200,
            Monto = 40000m,
            Estado = "Aprobado",
            Activo = true
        };
        db.PagosCuentaCorriente.Add(pagoAprobado);

        // Pago pendiente: 20.000 (aún no debe descontar)
        var pagoPendiente = new PagoCuentaCorriente
        {
            Id = 2,
            ClienteId = 200,
            Monto = 20000m,
            Estado = "Pendiente",
            Activo = true
        };
        db.PagosCuentaCorriente.Add(pagoPendiente);

        await db.SaveChangesAsync();

        var totalFacturado = await db.DetallesPedido
            .Where(d => d.PedidoId == 300)
            .SumAsync(d => d.Cantidad * d.PrecioUnitario);

        var pagosAprobados = await db.PagosCuentaCorriente
            .Where(p => p.ClienteId == 200 && p.Estado == "Aprobado" && p.Activo)
            .SumAsync(p => p.Monto);

        var saldoDeudor = Math.Max(0m, totalFacturado - pagosAprobados);

        Assert.Equal(100000m, totalFacturado);
        Assert.Equal(40000m, pagosAprobados);
        Assert.Equal(60000m, saldoDeudor);
    }

    [Fact]
    public async Task PagoCuentaCorriente_Rechazo_GuardaMotivo()
    {
        using var db = CrearContextoEnMemoria("Test_PagoCC_Rechazo");
        var pago = new PagoCuentaCorriente
        {
            Id = 10,
            ClienteId = 50,
            Monto = 15000m,
            Estado = "Pendiente",
            Activo = true
        };
        db.PagosCuentaCorriente.Add(pago);
        await db.SaveChangesAsync();

        // Simular rechazo por el administrador
        pago.Estado = "Rechazado";
        pago.MotivoRechazo = "Comprobante ilegible / Transferencia no acreditada";
        pago.AuditadoPorId = 1;
        pago.AuditadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var recuperado = await db.PagosCuentaCorriente.FirstAsync(p => p.Id == 10);
        Assert.Equal("Rechazado", recuperado.Estado);
        Assert.Equal("Comprobante ilegible / Transferencia no acreditada", recuperado.MotivoRechazo);
        Assert.Equal(1, recuperado.AuditadoPorId);
    }
}
