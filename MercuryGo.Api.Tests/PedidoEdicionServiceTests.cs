using MercuryGo.Api.Domain.Entities;
using MercuryGo.Api.Services;

namespace MercuryGo.Api.Tests;

// Reglas de edición de líneas: actualizar en su lugar en vez de borrar y reinsertar.
public sealed class PedidoEdicionServiceTests
{
    private static Producto Producto(long id, decimal precio, bool activo = true) => new()
    {
        Id = id,
        Nombre = $"Producto {id}",
        Precio = precio,
        Activo = activo
    };

    private static DetallePedido Detalle(long productoId, int cantidad, decimal precio, int preparada = 0) => new()
    {
        PedidoId = 1,
        ProductoId = productoId,
        Cantidad = cantidad,
        CantidadPedida = cantidad,
        CantidadPreparada = preparada,
        PrecioUnitario = precio
    };

    [Fact]
    public void ConservaPrecioHistoricoAlCambiarCantidad()
    {
        var actuales = new List<DetallePedido> { Detalle(10, 2, 150m, 2) };
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 999m) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, actuales, [new ItemEdicionPedido(10, 5)], productos);

        Assert.Null(resultado.Error);
        Assert.Equal(5, actuales[0].Cantidad);
        Assert.Equal(5, actuales[0].CantidadPedida);
        Assert.Equal(150m, actuales[0].PrecioUnitario);
        Assert.Empty(resultado.Agregados);
        Assert.Empty(resultado.Eliminados);
        Assert.Equal(750m, resultado.TotalResultante);
    }

    [Fact]
    public void RecortaCantidadPreparadaSiBajaLaCantidad()
    {
        var actuales = new List<DetallePedido> { Detalle(10, 8, 100m, 8) };
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 100m) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, actuales, [new ItemEdicionPedido(10, 3)], productos);

        Assert.Null(resultado.Error);
        Assert.Equal(3, actuales[0].Cantidad);
        Assert.Equal(3, actuales[0].CantidadPreparada);
    }

    [Fact]
    public void AgregaProductoNuevoConPrecioDeCatalogo()
    {
        var actuales = new List<DetallePedido> { Detalle(10, 1, 100m) };
        var productos = new Dictionary<long, Producto>
        {
            [10] = Producto(10, 100m),
            [20] = Producto(20, 250m)
        };

        var resultado = PedidoEdicionService.Reconciliar(
            1, actuales, [new ItemEdicionPedido(10, 1), new ItemEdicionPedido(20, 4)], productos);

        Assert.Null(resultado.Error);
        var agregado = Assert.Single(resultado.Agregados);
        Assert.Equal(20, agregado.ProductoId);
        Assert.Equal(4, agregado.CantidadPedida);
        Assert.Equal(250m, agregado.PrecioUnitario);
        Assert.Equal(1100m, resultado.TotalResultante);
    }

    [Fact]
    public void EliminaSoloLaLineaQuitada()
    {
        var actuales = new List<DetallePedido> { Detalle(10, 2, 100m), Detalle(20, 1, 300m) };
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 100m) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, actuales, [new ItemEdicionPedido(10, 2)], productos);

        Assert.Null(resultado.Error);
        var eliminado = Assert.Single(resultado.Eliminados);
        Assert.Equal(20, eliminado.ProductoId);
        Assert.Equal(200m, resultado.TotalResultante);
    }

    [Fact]
    public void RechazaProductoRepetido()
    {
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 100m) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, [], [new ItemEdicionPedido(10, 1), new ItemEdicionPedido(10, 2)], productos);

        Assert.NotNull(resultado.Error);
        Assert.Equal("items_duplicados", resultado.Error!.Codigo);
    }

    [Fact]
    public void RechazaCantidadInvalida()
    {
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 100m) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, [], [new ItemEdicionPedido(10, 0)], productos);

        Assert.NotNull(resultado.Error);
        Assert.Equal("cantidad_invalida", resultado.Error!.Codigo);
    }

    [Fact]
    public void RechazaProductoInexistenteOInactivo()
    {
        var productos = new Dictionary<long, Producto> { [10] = Producto(10, 100m, activo: false) };

        var resultado = PedidoEdicionService.Reconciliar(
            1, [], [new ItemEdicionPedido(10, 1)], productos);

        Assert.NotNull(resultado.Error);
        Assert.Equal("producto_invalido", resultado.Error!.Codigo);
    }

    [Fact]
    public void RechazaListaVacia()
    {
        var resultado = PedidoEdicionService.Reconciliar(
            1, [Detalle(10, 1, 100m)], [], new Dictionary<long, Producto>());

        Assert.NotNull(resultado.Error);
        Assert.Equal("items_requeridos", resultado.Error!.Codigo);
    }
}
