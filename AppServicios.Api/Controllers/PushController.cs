using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AppServicios.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PushController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly IConfiguration _configuration;

        public PushController(AppServiciosDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet("public-key")]
        public ActionResult<object> GetPublicKey()
        {
            var publicKey = _configuration["VAPID:PublicKey"];
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                return NotFound(new { message = "Las notificaciones push no están configuradas." });
            }

            return Ok(new { publicKey });
        }

        public sealed class NativeTokenRequest
        {
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.StringLength(4096, MinimumLength = 20)]
            [System.ComponentModel.DataAnnotations.RegularExpression(@"[A-Za-z0-9_:\-]+")]
            public string Token { get; set; } = string.Empty;
        }

        [HttpGet("native-status")]
        public IActionResult NativeStatus() => Ok(new {
            configured = !string.IsNullOrWhiteSpace(_configuration["Firebase:CredentialsPath"])
                && System.IO.File.Exists(_configuration["Firebase:CredentialsPath"])
        });

        [HttpPost("native-token")]
        public async Task<IActionResult> RegisterNativeToken(NativeTokenRequest request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            var endpoint = "fcm:" + request.Token;
            var rows = await _context.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync();
            var subscription = rows.FirstOrDefault();
            if (subscription is null) {
                subscription = new PushSubscription { Endpoint = endpoint };
                _context.PushSubscriptions.Add(subscription);
            }
            else _context.PushSubscriptions.RemoveRange(rows.Skip(1));
            // A device belongs only to the account currently signed in on it.
            subscription.UsuarioId = userId;
            subscription.P256dh = string.Empty;
            subscription.Auth = string.Empty;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("native-unsubscribe")]
        public async Task<IActionResult> UnsubscribeNative(NativeTokenRequest request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            await _context.PushSubscriptions.Where(s => s.UsuarioId == userId && s.Endpoint == "fcm:" + request.Token).ExecuteDeleteAsync();
            return Ok();
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto dto)
        {
            var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            var claimUserId = int.TryParse(rawUserId, out var userId) ? userId : 0;
            if (claimUserId != dto.UsuarioId)
                return Forbid();

            var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(p => p.UsuarioId == dto.UsuarioId && p.Endpoint == dto.Endpoint);
            if (existing == null)
            {
                var sub = new PushSubscription
                {
                    UsuarioId = dto.UsuarioId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth
                };
                _context.PushSubscriptions.Add(sub);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}
