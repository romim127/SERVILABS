using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using AppServicios.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AppServicios.Api.Domain;
using Microsoft.Extensions.Configuration;

namespace AppServicios.Api.Services
{
    public class PushNotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly Lazy<FirebaseMessaging?> _messaging;
        public bool NativeConfigured => !string.IsNullOrWhiteSpace(_config["Firebase:CredentialsPath"]);

        public PushNotificationService(IConfiguration config, ILogger<PushNotificationService> logger)
        {
            _config = config;
            _logger = logger;
            _messaging = new Lazy<FirebaseMessaging?>(() => {
                var path = config["Firebase:CredentialsPath"];
                if (string.IsNullOrWhiteSpace(path)) return null;
                var app = FirebaseApp.Create(new AppOptions {
                    Credential = CredentialFactory.FromFile<ServiceAccountCredential>(path).ToGoogleCredential(),
                    ProjectId = config["Firebase:ProjectId"] ?? "servilabs-fe09a"
                }, "servilabs-push");
                return FirebaseMessaging.GetMessaging(app);
            });
        }

        public async Task SendAsync(
            PushSubscription sub,
            string title,
            string body,
            string? url = null,
            string? icon = null,
            string? badge = null)
        {
            if (sub.Endpoint.StartsWith("fcm:", StringComparison.Ordinal))
            {
                try
                {
                    var messaging = _messaging.Value;
                    if (messaging is null) return;
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await messaging.SendAsync(new Message {
                        Token = sub.Endpoint[4..],
                        Notification = new Notification { Title = title, Body = body },
                        Data = new Dictionary<string, string> { ["url"] = url ?? "/#cuenta" },
                        Android = new AndroidConfig {
                            Priority = Priority.High,
                            Notification = new AndroidNotification { ChannelId = "servilabs_updates", Sound = "default" }
                        }
                    }, timeout.Token);
                }
                catch (Exception ex)
                {
                    // Never log a device token or provider response containing credentials.
                    _logger.LogWarning("Native push failed for user {UserId}: {ErrorType}", sub.UsuarioId, ex.GetType().Name);
                }
                return;
            }
            // Configuración VAPID
            var vapidPublicKey = _config["VAPID:PublicKey"];
            var vapidPrivateKey = _config["VAPID:PrivateKey"];
            var subject = _config["VAPID:Subject"] ?? "mailto:admin@tudominio.com";

            if (string.IsNullOrWhiteSpace(vapidPublicKey) || string.IsNullOrWhiteSpace(vapidPrivateKey))
            {
                return;
            }

            // Payload
            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                url,
                icon,
                badge
            });

            // Usar WebPushNet (instalar paquete WebPush)
            var webPushClient = new WebPush.WebPushClient();
            var vapidDetails = new WebPush.VapidDetails(subject, vapidPublicKey, vapidPrivateKey);
            var subscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            try
            {
                await webPushClient.SendNotificationAsync(subscription, payload, vapidDetails);
            }
            catch (WebPush.WebPushException)
            {
                // Manejo de error: endpoint inválido, eliminar suscripción, etc.
                // Log o acción según corresponda
            }
        }
        public async Task SendToUserAsync(AppServiciosDbContext context, int userId, string title, string body, string url)
        {
            if (!await context.Usuarios.AnyAsync(u => u.Id == userId && u.Activo && u.RecibeNotificaciones)) return;
            var subscriptions = await context.PushSubscriptions.AsNoTracking().Where(s => s.UsuarioId == userId).ToListAsync();
            foreach (var subscription in subscriptions) await SendAsync(subscription, title, body, url);
        }
    }
}
