namespace MercuryGo.Api.Domain.Entities;

// Token de larga vida que permite renovar el access token (JWT) sin volver a pedir contraseña.
// Se almacena hasheado (SHA-256): si alguien accede a la tabla, no puede usar los tokens.
// Aplica rotación obligatoria: en cada renovación, el token presentado se marca como revocado
// y se emite uno nuevo.
public sealed class RefreshToken : EntityBase
{
    public long UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
}
