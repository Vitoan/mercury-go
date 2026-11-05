using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Auth;
using MercuryGo.Api.Controllers;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using Xunit;

namespace MercuryGo.Api.Tests;

public sealed class DispositivosTests
{
    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    private static ControllerContext CrearContextoConUsuario(long usuarioId, string rol = RolCodigos.Operario)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim(ClaimTypes.Role, rol)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static SesionController CrearSesionController(MercuryGoDbContext db)
    {
        var limiter = new LoginRateLimiterService();
        var tokenService = new TokenService(new JwtOpciones { Key = "MercuryGo_Super_Secret_Key_For_Unit_Testing_32chars!" });
        return new SesionController(db, tokenService, limiter);
    }

    [Fact]
    public async Task Registrar_CreaNuevoDispositivo_SiTokenNoExiste()
    {
        using var db = CrearContextoEnMemoria("Test_Dispositivo_Crear");
        var controller = new DispositivosController(db)
        {
            ControllerContext = CrearContextoConUsuario(100)
        };

        var resultado = await controller.Registrar(new RegistrarDispositivoRequest("fcm-token-123"), CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(okResult.Value);

        var guardado = await db.DispositivosPush.FirstOrDefaultAsync(d => d.Token == "fcm-token-123");
        Assert.NotNull(guardado);
        Assert.Equal(100, guardado.UsuarioId);
    }

    [Fact]
    public async Task Registrar_ReasignaDispositivo_SiTokenYaExistiaParaOtroUsuario()
    {
        using var db = CrearContextoEnMemoria("Test_Dispositivo_Reasignar");
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 100, Token = "fcm-token-compartido" });
        await db.SaveChangesAsync();

        var controller = new DispositivosController(db)
        {
            ControllerContext = CrearContextoConUsuario(200)
        };

        var resultado = await controller.Registrar(new RegistrarDispositivoRequest("fcm-token-compartido"), CancellationToken.None);
        Assert.IsType<OkObjectResult>(resultado);

        var dispositivos = await db.DispositivosPush.Where(d => d.Token == "fcm-token-compartido").ToListAsync();
        Assert.Single(dispositivos);
        Assert.Equal(200, dispositivos[0].UsuarioId);
    }

    [Fact]
    public async Task Registrar_RetornaBadRequest_SiTokenVacio()
    {
        using var db = CrearContextoEnMemoria("Test_Dispositivo_TokenVacio");
        var controller = new DispositivosController(db)
        {
            ControllerContext = CrearContextoConUsuario(100)
        };

        var resultado = await controller.Registrar(new RegistrarDispositivoRequest("   "), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task Logout_DesvinculaTokenPush_CuandoSeProporciona()
    {
        using var db = CrearContextoEnMemoria("Test_Logout_DesvincularToken");
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 100, Token = "token-a-eliminar" });
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 100, Token = "token-otro-dispositivo" });
        
        var refreshToken = new RefreshToken
        {
            UsuarioId = 100,
            TokenHash = TokenService.HashRefresh("test-refresh-123"),
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(7)
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var sesionController = CrearSesionController(db);

        var resultado = await sesionController.Logout(
            new LogoutRequest("test-refresh-123", Todos: false, TokenPush: "token-a-eliminar"),
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(resultado);

        var tokensRestantes = await db.DispositivosPush.Where(d => d.UsuarioId == 100).ToListAsync();
        Assert.Single(tokensRestantes);
        Assert.Equal("token-otro-dispositivo", tokensRestantes[0].Token);
    }

    [Fact]
    public async Task Logout_Todos_EliminaTodosLosDispositivosDelUsuario()
    {
        using var db = CrearContextoEnMemoria("Test_Logout_Todos");
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 500, Token = "token-1" });
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 500, Token = "token-2" });
        db.DispositivosPush.Add(new DispositivoPush { UsuarioId = 600, Token = "token-otro-user" });

        var refreshToken = new RefreshToken
        {
            UsuarioId = 500,
            TokenHash = TokenService.HashRefresh("test-refresh-500"),
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(7)
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        var sesionController = CrearSesionController(db);

        var resultado = await sesionController.Logout(
            new LogoutRequest("test-refresh-500", Todos: true),
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(resultado);

        var tokens500 = await db.DispositivosPush.Where(d => d.UsuarioId == 500).ToListAsync();
        Assert.Empty(tokens500);

        var token600 = await db.DispositivosPush.FirstOrDefaultAsync(d => d.UsuarioId == 600);
        Assert.NotNull(token600);
    }
}
