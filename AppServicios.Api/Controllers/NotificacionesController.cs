using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class NotificacionesController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly Services.PushNotificationService _pushService;

        public NotificacionesController(AppServiciosDbContext context, Services.PushNotificationService pushService)
        {
            _context = context;
            _pushService = pushService;
        }

        [HttpGet("usuario/{usuarioId:int}")]
        public async Task<ActionResult<IEnumerable<NotificacionDto>>> GetByUsuario(int usuarioId)
        {
            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue)
            {
                return Unauthorized("Token inválido o sesión expirada.");
            }

            if (claimUserId.Value != usuarioId && !IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var items = await _context.Notificaciones
                .AsNoTracking()
                .Where(n => n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.FechaCreacion)
                .Take(30)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpPost("{id:int}/marcar-leida")]
        public async Task<ActionResult<NotificacionDto>> MarcarLeida(int id)
        {
            var item = await _context.Notificaciones.FirstOrDefaultAsync(n => n.Id == id);
            if (item is null)
            {
                return NotFound();
            }

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue)
            {
                return Unauthorized("Token inválido o sesión expirada.");
            }

            if (item.UsuarioId != claimUserId.Value && !IsCurrentUserAdmin())
            {
                return Forbid();
            }

            item.Leida = true;
            await _context.SaveChangesAsync();

            return Ok(ToDto(item));
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private bool IsCurrentUserAdmin() => User.IsInRole("Administrador");

        private static NotificacionDto ToDto(AppServicios.Api.Domain.Notificacion item) => new(
            item.Id,
            item.UsuarioId,
            item.Titulo,
            item.Mensaje,
            item.Tipo,
            item.SolicitudTrabajoId,
            item.Leida,
            item.FechaCreacion);
    }
}
