namespace MercuryGo.Api.Domain.Entities;

// RolId es opcional (long?): un usuario recién registrado puede crearse sin rol.
// Queda autenticado (con token válido) pero no autorizado a módulos operativos
// hasta que un Administrador le asigne un rol.
// La contraseña nunca se guarda en texto plano: se almacena su hash PBKDF2.
public sealed class Usuario : EntityBase
{
    public long? RolId { get; set; }
    public Rol? Rol { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public bool Activo { get; set; } = true;
}
