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
        public PushNotificationService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(
            PushSubscription sub,
            string title,
            string body,
            string? url = null,
            string? icon = null,
            string? badge = null)
        {
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
    }
}
