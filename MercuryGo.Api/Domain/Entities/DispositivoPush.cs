namespace MercuryGo.Api.Domain.Entities;

// Dispositivo móvil donde el usuario tiene su sesión abierta, identificado por su token de FCM.
// Es la dirección física de red a la que la API despacha las notificaciones push.
// Un usuario puede tener múltiples dispositivos; cada token pertenece a un único usuario a la vez.
public sealed class DispositivoPush : EntityBase
{
    public long UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Token { get; set; } = string.Empty;
}
