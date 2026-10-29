namespace MercuryGo.Api.Tests;

using Xunit;
using MercuryGo.Api.Auth;

public sealed class LoginRateLimiterTests
{
    [Fact]
    public async Task BloqueaTrasCincoIntentosFallidos()
    {
        var limiter = new LoginRateLimiterService(maximosIntentos: 5, minutosBloqueo: 15);
        var ip = "192.168.1.50";
        var email = "admin@mercurygo.com";

        for (int i = 0; i < 4; i++)
        {
            await limiter.RegistrarFalloAsync(ip, email);
            var bloqueadoAntes = await limiter.EstaBloqueadoAsync(ip, email);
            Assert.False(bloqueadoAntes, $"No debe bloquear en intento {i + 1}");
        }

        // Quinto fallo
        await limiter.RegistrarFalloAsync(ip, email);
        var bloqueadoDespues = await limiter.EstaBloqueadoAsync(ip, email);
        Assert.True(bloqueadoDespues, "Debe bloquear tras 5 intentos fallidos.");
    }

    [Fact]
    public async Task LimpiarRestableceElAcceso()
    {
        var limiter = new LoginRateLimiterService(maximosIntentos: 3, minutosBloqueo: 15);
        var ip = "10.0.0.1";
        var email = "chofer@mercurygo.com";

        for (int i = 0; i < 3; i++)
        {
            await limiter.RegistrarFalloAsync(ip, email);
        }

        Assert.True(await limiter.EstaBloqueadoAsync(ip, email));

        await limiter.LimpiarAsync(ip, email);

        Assert.False(await limiter.EstaBloqueadoAsync(ip, email));
    }

    [Fact]
    public async Task NoBloqueaAUsuariosDiferentes()
    {
        var limiter = new LoginRateLimiterService(maximosIntentos: 3, minutosBloqueo: 15);
        var ip = "127.0.0.1";

        for (int i = 0; i < 3; i++)
        {
            await limiter.RegistrarFalloAsync(ip, "atacante@mercurygo.com");
        }

        Assert.True(await limiter.EstaBloqueadoAsync(ip, "atacante@mercurygo.com"));
        Assert.False(await limiter.EstaBloqueadoAsync(ip, "usuario_valido@mercurygo.com"));
    }
}
