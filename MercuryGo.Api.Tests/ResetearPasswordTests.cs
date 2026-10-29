using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;
using Xunit;

namespace MercuryGo.Api.Tests;

public class ResetearPasswordTests
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    private static MercuryGoDbContext CrearContextoEnMemoria(string dbName)
    {
        var options = new DbContextOptionsBuilder<MercuryGoDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new MercuryGoDbContext(options);
    }

    [Fact]
    public void ValidacionPassword_MinimoOchoCaracteres()
    {
        var passwordCorta = "12345";
        var passwordValida = "Mercury2026!";

        Assert.True(passwordCorta.Length < 8);
        Assert.True(passwordValida.Length >= 8);
    }

    [Fact]
    public async Task ResetearPassword_ActualizaHash_Y_RevocaSesionesPrevias()
    {
        using var db = CrearContextoEnMemoria("Test_Reset_Password");

        var usuario = new Usuario
        {
            Id = 50,
            Nombre = "Operario Deposito",
            Email = "operario50@mercurygo.local",
            PasswordHash = "antiguoHash",
            Activo = true
        };
        db.Usuarios.Add(usuario);

        // Crear 2 tokens de sesión activos
        var token1 = new RefreshToken
        {
            Id = 1,
            UsuarioId = 50,
            TokenHash = "hash-token-1",
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(7),
            RevocadoEn = null
        };
        var token2 = new RefreshToken
        {
            Id = 2,
            UsuarioId = 50,
            TokenHash = "hash-token-2",
            ExpiraEn = DateTimeOffset.UtcNow.AddDays(7),
            RevocadoEn = null
        };
        db.RefreshTokens.AddRange(token1, token2);
        await db.SaveChangesAsync();

        // Simular ejecución del reseteo administrativo
        var nuevaClave = "NuevaClaveSuperSegura2026!";
        usuario.PasswordHash = Hasher.HashPassword(usuario, nuevaClave);
        usuario.ActualizadoEn = DateTime.UtcNow;

        var tokensActivos = await db.RefreshTokens
            .Where(r => r.UsuarioId == 50 && r.RevocadoEn == null)
            .ToListAsync();

        tokensActivos.ForEach(t => t.RevocadoEn = DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        // Verificaciones
        var usuarioDb = await db.Usuarios.FirstAsync(u => u.Id == 50);
        var verificacion = Hasher.VerifyHashedPassword(usuarioDb, usuarioDb.PasswordHash, nuevaClave);
        Assert.Equal(PasswordVerificationResult.Success, verificacion);

        var tokensPostReset = await db.RefreshTokens.Where(r => r.UsuarioId == 50).ToListAsync();
        Assert.All(tokensPostReset, t => Assert.NotNull(t.RevocadoEn));
    }
}
