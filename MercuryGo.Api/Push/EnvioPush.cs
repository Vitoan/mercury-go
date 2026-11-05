using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using MercuryGo.Api.Data;
using MercuryGo.Api.Domain.Entities;

namespace MercuryGo.Api.Push;

// Servicio de despacho de notificaciones push a través de Firebase Cloud Messaging (FCM).
// La API se autentica con la cuenta de servicio de Firebase (clave privada).
// Si no se encuentra configurada, la API opera normalmente con la bandeja in-app sin arrojar errores.
public sealed class EnvioPush
{
    private readonly FirebaseMessaging? mensajeria;
    private readonly ILogger<EnvioPush> logger;

    public EnvioPush(IConfiguration configuracion, IHostEnvironment entorno, ILogger<EnvioPush> logger)
    {
        this.logger = logger;
        var ruta = configuracion["Firebase:CuentaDeServicio"];
        if (string.IsNullOrWhiteSpace(ruta))
        {
            logger.LogInformation("Push apagado: falta Firebase:CuentaDeServicio en configuración. La bandeja in-app sigue funcionando.");
            return;
        }

        try
        {
            var rutaCompleta = Path.IsPathRooted(ruta)
                ? ruta
                : Path.Combine(entorno.ContentRootPath, ruta);

            if (!File.Exists(rutaCompleta))
            {
                logger.LogWarning("Archivo de cuenta de servicio de Firebase no encontrado en {Ruta}. Push desactivado.", rutaCompleta);
                return;
            }

            var credencial = CredentialFactory.FromFile<ServiceAccountCredential>(rutaCompleta).ToGoogleCredential();
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credencial });
            mensajeria = FirebaseMessaging.GetMessaging(app);
            logger.LogInformation("Firebase Messaging inicializado exitosamente.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al inicializar Firebase Admin SDK. Push desactivado.");
        }
    }

    // Despacha una push por cada notificación a todos los dispositivos registrados del usuario.
    // El id del pedido viaja en data['pedido_id'] para permitir deep linking nativo al tocar la alerta.
    public async Task EnviarAsync(MercuryGoDbContext db, IReadOnlyCollection<Notificacion> notificaciones, CancellationToken cancellationToken)
    {
        if (mensajeria is null || notificaciones.Count == 0) return;

        var usuarios = notificaciones.Select(x => x.UsuarioId).Distinct().ToList();
        var dispositivos = await db.DispositivosPush.Where(x => usuarios.Contains(x.UsuarioId)).ToListAsync(cancellationToken);
        if (dispositivos.Count == 0) return;

        var vencidos = new List<DispositivoPush>();

        foreach (var notificacion in notificaciones)
        {
            var destino = dispositivos.Where(x => x.UsuarioId == notificacion.UsuarioId).ToList();
            if (destino.Count == 0) continue;

            var pedidoIdStr = notificacion.ReferenciaId?.ToString() ?? string.Empty;

            var respuesta = await mensajeria.SendEachAsync(destino.Select(dispositivo => new Message
            {
                Token = dispositivo.Token,
                Notification = new Notification
                {
                    Title = notificacion.Titulo,
                    Body = notificacion.Mensaje
                },
                Data = new Dictionary<string, string>
                {
                    ["pedido_id"] = pedidoIdStr,
                    ["tipo"] = notificacion.Tipo ?? "INFO"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "avisos"
                    }
                }
            }), cancellationToken);

            for (var i = 0; i < respuesta.Responses.Count; i++)
            {
                var error = respuesta.Responses[i].Exception;
                if (error is null) continue;

                if (error.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    vencidos.Add(destino[i]);
                }
                else
                {
                    logger.LogWarning(error, "FCM no pudo entregar la notificación push al dispositivo del usuario {UsuarioId}.", notificacion.UsuarioId);
                }
            }
        }

        if (vencidos.Count > 0)
        {
            db.DispositivosPush.RemoveRange(vencidos.Distinct());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
