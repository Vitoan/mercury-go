using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Services;
using Xunit;

namespace MercuryGo.Api.Tests;

public class IdempotenciaServiceTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public async Task ClaveInexistente_RetornaNull()
    {
        // Arrange
        using var db = CrearContextoEnMemoria("Test_Idemp_Inexistente");
        var servicio = new IdempotenciaService(db);

        // Act
        var op = await servicio.ObtenerOperacionAsync("clave-random-123", "ENTREGAR");

        // Assert
        Assert.Null(op);
    }

    [Fact]
    public async Task Registrar_Y_Obtener_RetornaMismoResultado()
    {
        // Arrange
        using var db = CrearContextoEnMemoria("Test_Idemp_Registrar");
        var servicio = new IdempotenciaService(db);
        var clave = "operacion-uuid-456";
        var resultadoEsperado = "{\"pedido_id\": 10, \"estado\": \"Entregado\"}";

        // Act
        await servicio.RegistrarOperacionAsync(
            idempotencyKey: clave,
            tipo: "ENTREGAR",
            usuarioId: 7,
            pedidoId: 10,
            resultadoJson: resultadoEsperado
        );

        var obtenido = await servicio.ObtenerOperacionAsync(clave, "ENTREGAR");

        // Assert
        Assert.NotNull(obtenido);
        Assert.Equal(clave, obtenido.Id);
        Assert.Equal(7, obtenido.UsuarioId);
        Assert.Equal("ENTREGAR", obtenido.Tipo);
        Assert.Equal(10, obtenido.PedidoId);
        Assert.Equal(resultadoEsperado, obtenido.ResultadoJson);
    }
}
