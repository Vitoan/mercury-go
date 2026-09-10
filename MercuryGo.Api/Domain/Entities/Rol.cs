namespace MercuryGo.Api.Domain.Entities;

// El rol es una fila en la base de datos MySQL y no un enum rígido en C#:
// un administrador puede consultarlos y asignarlos dinámicamente.
// El `Codigo` es lo que viaja en los claims del token JWT y valida las políticas de autorización.
public sealed class Rol : EntityBase
{
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
