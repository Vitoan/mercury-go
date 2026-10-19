using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Auth;

public sealed class JwtOpciones
{
    public const string Seccion = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MercuryGo.Api";
    public string Audience { get; set; } = "MercuryGoApp";
    public int HorasVigencia { get; set; } = 8;
    public int DiasVigenciaRefresh { get; set; } = 30;
}

public sealed class TokenService(JwtOpciones opciones)
{
    public const string ClaimRolCodigo = "rol_codigo";

    public SecurityKey ClaveFirma { get; } = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Key));

    public (string Token, DateTimeOffset ExpiraEn) Emitir(Usuario usuario, Rol? rol)
    {
        var expiraEn = DateTimeOffset.UtcNow.AddHours(opciones.HorasVigencia);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.Nombre),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Sin rol, el token existe pero no contiene claims de autorización.
        // El usuario está autenticado pero no autorizado.
        if (rol is not null)
        {
            claims.Add(new Claim(ClaimRolCodigo, rol.Codigo));
            claims.Add(new Claim(ClaimTypes.Role, rol.Codigo));
        }

        var token = new JwtSecurityToken(
            issuer: opciones.Issuer,
            audience: opciones.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiraEn.UtcDateTime,
            signingCredentials: new SigningCredentials(ClaveFirma, SecurityAlgorithms.HmacSha256)
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }

    // El Refresh Token es una cadena opaca de 48 bytes aleatorios.
    // Solo viaja al cliente al emitirse; en base de datos se guarda exclusivamente su hash SHA-256.
    public (string Token, string Hash, DateTimeOffset ExpiraEn) EmitirRefresh()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return (token, HashRefresh(token), DateTimeOffset.UtcNow.AddDays(opciones.DiasVigenciaRefresh));
    }

    public static string HashRefresh(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    // Clave efímera en desarrollo si no se especificó Jwt:Key
    public static string GenerarClaveEfimera()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
