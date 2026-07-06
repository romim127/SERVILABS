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
