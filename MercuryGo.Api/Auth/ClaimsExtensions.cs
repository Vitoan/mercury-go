using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MercuryGo.Api.Auth;

// Constantes oficiales de los roles del dominio logístico B2B de MercuryGO.
public static class RolCodigos
{
    public const string Admin = "ADMIN";
    public const string Operario = "OPERARIO";
    public const string Chofer = "CHOFER";
    public const string Cliente = "CLIENTE";
}

// Extensiones para extraer la identidad directamente del JWT validado.
// La identidad sale del token firmado y de ningún otro lado.
public static class ClaimsExtensions
{
    public static long UsuarioId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                 ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(valor, out var id) ? id : 0;
    }

    public static string? RolCodigo(this ClaimsPrincipal usuario)
    {
        return usuario.FindFirstValue(TokenService.ClaimRolCodigo)
            ?? usuario.FindFirstValue(ClaimTypes.Role);
    }

    public static bool EsAdmin(this ClaimsPrincipal usuario)
    {
        return usuario.IsInRole(RolCodigos.Admin);
    }
}
