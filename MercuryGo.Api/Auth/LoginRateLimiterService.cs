namespace MercuryGo.Api.Auth;

using System.Collections.Concurrent;

public interface ILoginRateLimiter
{
    ValueTask<bool> EstaBloqueadoAsync(string? ip, string? email);
    ValueTask RegistrarFalloAsync(string? ip, string? email);
    ValueTask LimpiarAsync(string? ip, string? email);
}

public sealed class LoginRateLimiterService : ILoginRateLimiter
{
    private sealed record IntentoRegistro(int Intentos, DateTime UltimoFallo);

    private readonly ConcurrentDictionary<string, IntentoRegistro> _intentos = new();
    private readonly int _maximosIntentos;
    private readonly TimeSpan _ventanaBloqueo;

    public LoginRateLimiterService(int maximosIntentos = 5, int minutosBloqueo = 15)
    {
        _maximosIntentos = maximosIntentos;
        _ventanaBloqueo = TimeSpan.FromMinutes(minutosBloqueo);
    }

    public ValueTask<bool> EstaBloqueadoAsync(string? ip, string? email)
    {
        var clave = GenerarClave(ip, email);
        if (_intentos.TryGetValue(clave, out var registro))
        {
            if (DateTime.UtcNow - registro.UltimoFallo < _ventanaBloqueo)
            {
                if (registro.Intentos >= _maximosIntentos)
                {
                    return ValueTask.FromResult(true);
                }
            }
            else
            {
                // La ventana expiró, limpiamos el contador
                _intentos.TryRemove(clave, out _);
            }
        }
        return ValueTask.FromResult(false);
    }

    public ValueTask RegistrarFalloAsync(string? ip, string? email)
    {
        var clave = GenerarClave(ip, email);
        var ahora = DateTime.UtcNow;

        _intentos.AddOrUpdate(
            clave,
            _ => new IntentoRegistro(1, ahora),
            (_, anterior) =>
            {
                if (ahora - anterior.UltimoFallo >= _ventanaBloqueo)
                {
                    return new IntentoRegistro(1, ahora);
                }
                return new IntentoRegistro(anterior.Intentos + 1, ahora);
            }
        );

        return ValueTask.CompletedTask;
    }

    public ValueTask LimpiarAsync(string? ip, string? email)
    {
        var clave = GenerarClave(ip, email);
        _intentos.TryRemove(clave, out _);
        return ValueTask.CompletedTask;
    }

    private static string GenerarClave(string? ip, string? email)
    {
        var ipNorm = (ip ?? "unknown").Trim().ToLowerInvariant();
        var emailNorm = (email ?? "unknown").Trim().ToLowerInvariant();
        return $"{ipNorm}:{emailNorm}";
    }
}
