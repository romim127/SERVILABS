using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.DTOs;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class MensajesSolicitudController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        private readonly Services.PushNotificationService _pushService;

        public MensajesSolicitudController(AppServiciosDbContext context, Services.PushNotificationService pushService)
        {
            _context = context;
            _pushService = pushService;
        }

        [HttpGet("solicitud/{solicitudId:int}")]
        public async Task<ActionResult<IEnumerable<MensajeSolicitudDto>>> GetBySolicitud(int solicitudId, [FromQuery] int userId)
        {
            var solicitud = await _context.SolicitudesTrabajo
                .AsNoTracking()
                .Include(s => s.Cliente)
                    .ThenInclude(c => c.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(p => p!.Usuario)
                .FirstOrDefaultAsync(s => s.Id == solicitudId);

            if (solicitud is null)
            {
                return NotFound(new { message = "La solicitud indicada no existe." });
            }

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue)
            {
                return Unauthorized("Token inválido o sesión expirada.");
            }

            if (claimUserId.Value != userId && !IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var actor = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (actor is null || !actor.Activo)
            {
                return Unauthorized("Debes iniciar sesión con una cuenta activa para ver esta conversación.");
            }

            if (!CanAccessConversation(solicitud, actor))
            {
                return Unauthorized("Solo las partes de esta solicitud o un administrador pueden ver esta conversación.");
            }

            var items = await _context.MensajesSolicitud
                .AsNoTracking()
                .Where(m => m.SolicitudTrabajoId == solicitudId)
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpPost]
        public async Task<ActionResult<MensajeSolicitudDto>> Create([FromBody] MensajeSolicitudCreateDto request)
        {
            var solicitud = await _context.SolicitudesTrabajo
                .Include(s => s.Cliente)
                    .ThenInclude(c => c.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(p => p!.Usuario)
                .Include(s => s.Servicio)
                .FirstOrDefaultAsync(s => s.Id == request.SolicitudTrabajoId);

            if (solicitud is null)
            {
                return NotFound(new { message = "La solicitud indicada no existe." });
            }

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue)
            {
                return Unauthorized("Token inválido o sesión expirada.");
            }

            if (claimUserId.Value != request.UsuarioId && !IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var sender = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId);
            if (sender is null)
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario indicado no existe.");
                return ValidationProblem(ModelState);
            }

            if (!sender.Activo)
            {
                return Unauthorized("Tu cuenta está suspendida. No puedes participar en esta conversación.");
            }

            if (!CanAccessConversation(solicitud, sender))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "Solo el cliente, el profesional asignado o un administrador pueden escribir en esta conversación.");
                return ValidationProblem(ModelState);
            }

            var trimmedContent = request.Contenido.Trim();
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                ModelState.AddModelError(nameof(request.Contenido), "El mensaje no puede estar vacío.");
                return ValidationProblem(ModelState);
            }

            var message = new MensajeSolicitud
            {
                SolicitudTrabajoId = solicitud.Id,
                UsuarioId = sender.Id,
                RemitenteNombre = sender.Nombre,
                Contenido = trimmedContent,
                FechaEnvio = DateTime.UtcNow
            };

            _context.MensajesSolicitud.Add(message);
            await _context.SaveChangesAsync();
            await CreateCounterpartNotificationAsync(solicitud, sender, request.Contenido.Trim());
            await AuditoriaHelper.RegistrarAsync(
                _context,
                sender.Id,
                "Chat",
                "Mensaje",
                $"{sender.Nombre} envió un mensaje en la solicitud #{solicitud.Id}.",
                "MensajeSolicitud",
                message.Id,
                solicitud.Servicio?.Nombre);

            return CreatedAtAction(nameof(GetBySolicitud), new { solicitudId = solicitud.Id }, ToDto(message));
        }

        private static bool CanAccessConversation(SolicitudTrabajo solicitud, Usuario actor)
        {
            if (string.Equals(actor.Rol, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return solicitud.Cliente?.UsuarioId == actor.Id
                || solicitud.Profesional?.UsuarioId == actor.Id;
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private bool IsCurrentUserAdmin() => User.IsInRole("Administrador");

        private async Task CreateCounterpartNotificationAsync(SolicitudTrabajo solicitud, Usuario sender, string contenido)
        {
            var recipientUserId = solicitud.Cliente?.UsuarioId == sender.Id
                ? solicitud.Profesional?.UsuarioId
                : solicitud.Cliente?.UsuarioId;

            if (!recipientUserId.HasValue)
            {
                return;
            }

            var recipient = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == recipientUserId.Value);
            if (recipient is null || !recipient.RecibeNotificaciones)
            {
                return;
            }

            var serviceName = solicitud.Servicio?.Nombre ?? "la solicitud";
            var preview = contenido.Length > 90 ? $"{contenido[..87]}..." : contenido;

            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId = recipient.Id,
                Titulo = "Nuevo mensaje en tu solicitud",
                Mensaje = $"{sender.Nombre} te escribió sobre {serviceName}: \"{preview}\"",
                Tipo = "MensajeSolicitud",
                SolicitudTrabajoId = solicitud.Id,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await _pushService.SendToUserAsync(_context, recipient.Id, "Nuevo mensaje en tu solicitud",
                $"Tenés un mensaje nuevo sobre {serviceName}.", "/#chat-solicitud");
        }

        private static MensajeSolicitudDto ToDto(MensajeSolicitud item) => new(
            item.Id,
            item.SolicitudTrabajoId,
            item.UsuarioId,
            item.RemitenteNombre,
            item.Contenido,
            item.FechaEnvio);
    }
}