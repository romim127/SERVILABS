using System.Security.Claims;
using System.Text;
using AppServicios.Api.Data;
using AppServicios.Api.DTOs;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class CoordinacionController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly IConfiguration _configuration;

        public CoordinacionController(AppServiciosDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<CoordinacionDashboardDto>> GetDashboard(
            [FromQuery] int days = 30,
            [FromQuery] string? city = null,
            [FromQuery] int? rubroId = null,
            [FromQuery] int? adminUserId = null)
        {
            var payload = await BuildDashboardAsync(days, city, rubroId, adminUserId);
            return Ok(payload);
        }

        [HttpGet("reporte.csv")]
        public async Task<IActionResult> ExportReportCsv(
            [FromQuery] int days = 30,
            [FromQuery] string? city = null,
            [FromQuery] int? rubroId = null)
        {
            var dashboard = await BuildDashboardAsync(days, city, rubroId);

            var csv = new StringBuilder();
            csv.AppendLine("Seccion,Detalle,Valor");
            csv.AppendLine($"Resumen,Periodo,{dashboard.PeriodoDias} días");
            csv.AppendLine($"Resumen,Filtro ciudad,{EscapeCsv(dashboard.FiltroCiudad)}");
            csv.AppendLine($"Resumen,Filtro rubro,{EscapeCsv(dashboard.FiltroRubro)}");
            csv.AppendLine($"KPI,Total usuarios,{dashboard.TotalUsuarios}");
            csv.AppendLine($"KPI,Total solicitudes,{dashboard.TotalSolicitudes}");
            csv.AppendLine($"KPI,Solicitudes completadas,{dashboard.SolicitudesCompletadas}");
            csv.AppendLine($"KPI,Volumen gestionado,{dashboard.VolumenGestionado}");
            csv.AppendLine($"KPI,Pipeline pendiente,{dashboard.PipelinePendiente}");
            csv.AppendLine($"KPI,Ticket promedio,{dashboard.TicketPromedio}");
            csv.AppendLine($"KPI,Cumplimiento operativo,{dashboard.CumplimientoOperativo}");
            csv.AppendLine($"KPI,Activación profesional,{dashboard.ActivacionProfesional}");
            csv.AppendLine($"KPI,Tasa aceptación,{dashboard.TasaAceptacion}");
            csv.AppendLine($"KPI,SLA respuesta horas,{dashboard.SlaRespuestaHoras}");
            csv.AppendLine();
            csv.AppendLine("Rubros,Rubro,Solicitudes,TicketPromedio,TasaCierre");
            foreach (var item in dashboard.RubrosTop)
            {
                csv.AppendLine($"Rubros,{EscapeCsv(item.Rubro)},{item.Solicitudes},{item.TicketPromedio},{item.TasaCierre}");
            }
            csv.AppendLine();
            csv.AppendLine("Profesionales,Nombre,Rubros,Completados,VolumenMovido");
            foreach (var item in dashboard.ProfesionalesTop)
            {
                csv.AppendLine($"Profesionales,{EscapeCsv(item.Profesional)},{EscapeCsv(item.Rubros)},{item.TrabajosCompletados},{item.VolumenMovido}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"reporte-coordinacion-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("admin/usuarios/{userId:int}/accion")]
        public async Task<ActionResult<AdminUsuarioGestionDto>> UpdateUsuarioFromAdmin(int userId, [FromBody] AdminUsuarioAccionDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var adminUserId = GetAuthenticatedUserId();
            if (!adminUserId.HasValue || !await IsActiveSuperAdminAsync(adminUserId))
            {
                return Unauthorized("Solo el Super Admin activo puede ejecutar esta acción.");
            }

            if (request.AdminUserId > 0 && request.AdminUserId != adminUserId.Value)
            {
                return Forbid();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == userId);
            if (usuario is null)
            {
                return NotFound();
            }

            if (adminUserId.Value == userId && request.Activo.HasValue && !request.Activo.Value)
            {
                return BadRequest("No puedes suspender tu propio acceso administrador desde este panel.");
            }

            var cambios = new List<string>();

            if (request.Activo.HasValue && usuario.Activo != request.Activo.Value)
            {
                usuario.Activo = request.Activo.Value;
                cambios.Add(request.Activo.Value ? "reactivó la cuenta" : "suspendió la cuenta");
            }

            if (request.VerificadoRenaper.HasValue && usuario.VerificadoRenaper != request.VerificadoRenaper.Value)
            {
                usuario.VerificadoRenaper = request.VerificadoRenaper.Value;
                usuario.FechaVerificacion = request.VerificadoRenaper.Value ? DateTime.UtcNow : null;
                cambios.Add(request.VerificadoRenaper.Value ? "marcó identidad verificada" : "quitó la verificación de identidad");
            }

            if (request.RecibeNotificaciones.HasValue && usuario.RecibeNotificaciones != request.RecibeNotificaciones.Value)
            {
                usuario.RecibeNotificaciones = request.RecibeNotificaciones.Value;
                cambios.Add(request.RecibeNotificaciones.Value ? "habilitó notificaciones" : "silenció notificaciones");
            }

            if (cambios.Count == 0)
            {
                return BadRequest("No se detectaron cambios para aplicar en el usuario.");
            }

            await _context.SaveChangesAsync();

            var motivo = string.IsNullOrWhiteSpace(request.Motivo)
                ? string.Empty
                : $" Motivo: {request.Motivo.Trim()}.";

            await AuditoriaHelper.RegistrarAsync(
                _context,
                adminUserId.Value,
                "Administración",
                "Gestión de usuario",
                $"Admin {string.Join(", ", cambios)} sobre {usuario.Nombre}.{motivo}",
                "Usuario",
                usuario.Id,
                $"{usuario.Email} | activo={usuario.Activo} | notificaciones={usuario.RecibeNotificaciones} | verificado={usuario.VerificadoRenaper}");

            var actualizado = await BuildAdminUserAsync(userId);
            return actualizado is null ? NotFound() : Ok(actualizado);
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet("admin/ips-bloqueadas")]
        public async Task<ActionResult<IEnumerable<IpBloqueadaDto>>> GetIpsBloqueadas()
        {
            var adminUserId = GetAuthenticatedUserId();
            if (!adminUserId.HasValue || !await IsActiveSuperAdminAsync(adminUserId))
            {
                return Unauthorized("Solo el Super Admin activo puede consultar IPs bloqueadas.");
            }

            var items = await _context.IpsBloqueadas
                .AsNoTracking()
                .OrderByDescending(i => i.Activa)
                .ThenByDescending(i => i.FechaUltimoIntento ?? i.FechaCreacion)
                .Take(100)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost("admin/ips-bloqueadas/{id:int}/desbloquear")]
        public async Task<ActionResult<IpBloqueadaDto>> DesbloquearIp(int id, [FromBody] DesbloquearIpDto request)
        {
            var adminUserId = GetAuthenticatedUserId();
            if (!adminUserId.HasValue || !await IsActiveSuperAdminAsync(adminUserId))
            {
                return Unauthorized("Solo el Super Admin activo puede desbloquear IPs.");
            }

            var item = await _context.IpsBloqueadas.FirstOrDefaultAsync(i => i.Id == id);
            if (item is null)
            {
                return NotFound();
            }

            await UnlockIpAsync(item, $"super-admin:{adminUserId.Value}", request.Motivo);
            return Ok(ToDto(item));
        }

        [AllowAnonymous]
        [HttpPost("recovery/desbloquear-ip")]
        public async Task<ActionResult<IpBloqueadaDto>> RecoveryDesbloquearIp([FromBody] DesbloquearIpDto request)
        {
            var recoveryKey = _configuration["Security:RecoveryKey"];
            var provided = Request.Headers["X-Recovery-Key"].ToString();
            if (string.IsNullOrWhiteSpace(recoveryKey)
                || string.IsNullOrWhiteSpace(provided)
                || !string.Equals(recoveryKey, provided, StringComparison.Ordinal))
            {
                await AuditoriaHelper.RegistrarAsync(
                    _context,
                    null,
                    "Seguridad",
                    "Recovery rechazado",
                    "Intento inválido de recovery para desbloqueo de IP.",
                    "IpBloqueada",
                    null,
                    request.Ip);
                return Unauthorized("Recovery key inválida.");
            }

            var ip = request.Ip.Trim();
            var item = await _context.IpsBloqueadas.FirstOrDefaultAsync(i => i.Ip == ip);
            if (item is null)
            {
                return NotFound();
            }

            await UnlockIpAsync(item, "recovery-key", request.Motivo);
            return Ok(ToDto(item));
        }

        private async Task<CoordinacionDashboardDto> BuildDashboardAsync(int days, string? city, int? rubroId, int? adminUserId = null)
        {
            var normalizedDays = days switch
            {
                <= 7 => 7,
                <= 30 => 30,
                <= 90 => 90,
                _ => 180
            };

            var normalizedCity = (city ?? string.Empty).Trim();
            var now = DateTime.UtcNow;
            var recentWindow = now.AddDays(-normalizedDays);

            var totalUsuarios = await _context.Usuarios.AsNoTracking().CountAsync();
            var totalClientes = await _context.Clientes.AsNoTracking().CountAsync();
            var totalProfesionales = await _context.Profesionales.AsNoTracking().CountAsync();
            var pagosAprobados = await _context.PagosProfesionales.AsNoTracking()
                .Where(p => p.Estado == "Aprobado")
                .Select(p => p.UsuarioId)
                .Distinct()
                .CountAsync();
            var totalMensajes = await _context.MensajesSolicitud.AsNoTracking().CountAsync();
            var adminMode = await IsActiveAdminAsync(adminUserId);

            var solicitudesQuery = _context.SolicitudesTrabajo
                .AsNoTracking()
                .Include(s => s.Servicio)
                    .ThenInclude(servicio => servicio.Rubro)
                .Include(s => s.Cliente)
                    .ThenInclude(cliente => cliente.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(profesional => profesional!.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(profesional => profesional!.RubrosProfesionales)
                .Where(s => s.FechaCreacion >= recentWindow);

            if (!string.IsNullOrWhiteSpace(normalizedCity))
            {
                solicitudesQuery = solicitudesQuery.Where(s => s.Ubicacion.Contains(normalizedCity));
            }

            if (rubroId.HasValue)
            {
                solicitudesQuery = solicitudesQuery.Where(s => s.Servicio.RubroId == rubroId.Value);
            }

            var solicitudes = await solicitudesQuery
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync();

            var rubroName = "Todos los rubros";
            if (rubroId.HasValue)
            {
                rubroName = await _context.Rubros.AsNoTracking()
                    .Where(r => r.Id == rubroId.Value)
                    .Select(r => r.Nombre)
                    .FirstOrDefaultAsync() ?? $"Rubro #{rubroId.Value}";
            }

            var totalSolicitudes = solicitudes.Count;
            var solicitudesPendientes = solicitudes.Count(s => string.Equals(s.Estado, "Pendiente", StringComparison.OrdinalIgnoreCase));
            var solicitudesAceptadas = solicitudes.Count(s => string.Equals(s.Estado, "Aceptado", StringComparison.OrdinalIgnoreCase));
            var solicitudesCompletadas = solicitudes.Count(s => string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase));
            var solicitudesRechazadas = solicitudes.Count(s => string.Equals(s.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase));

            var volumenGestionado = solicitudes
                .Where(s => string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.PresupuestoFinal ?? s.PresupuestoEstimado);

            var pipelinePendiente = solicitudes
                .Where(s => !string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(s.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(s.Estado, "Cancelado", StringComparison.OrdinalIgnoreCase))
                .Sum(s => s.PresupuestoFinal ?? s.PresupuestoEstimado);

            var ticketPromedio = totalSolicitudes == 0
                ? 0m
                : Math.Round(solicitudes.Average(s => s.PresupuestoEstimado), 0);

            var cumplimientoOperativo = totalSolicitudes == 0
                ? 0m
                : Math.Round(solicitudesCompletadas * 100m / totalSolicitudes, 1);

            var activacionProfesional = totalProfesionales == 0
                ? 0m
                : Math.Round(pagosAprobados * 100m / totalProfesionales, 1);

            var tasaAceptacion = totalSolicitudes == 0
                ? 0m
                : Math.Round((solicitudesAceptadas + solicitudesCompletadas) * 100m / totalSolicitudes, 1);

            var acceptedWithDates = solicitudes
                .Where(s => s.FechaAceptacion.HasValue)
                .ToList();

            var slaRespuestaHoras = acceptedWithDates.Count == 0
                ? 0m
                : Math.Round((decimal)acceptedWithDates.Average(s => (s.FechaAceptacion!.Value - s.FechaCreacion).TotalHours), 1);

            var solicitudIds = solicitudes.Select(s => s.Id).ToList();
            var chatsActivos = solicitudIds.Count == 0
                ? 0
                : await _context.MensajesSolicitud.AsNoTracking()
                    .Where(m => solicitudIds.Contains(m.SolicitudTrabajoId))
                    .Select(m => m.SolicitudTrabajoId)
                    .Distinct()
                    .CountAsync();

            var demandMeta = normalizedDays switch
            {
                7 => 3m,
                30 => 6m,
                90 => 14m,
                _ => 24m
            };

            var chatMeta = normalizedDays switch
            {
                7 => 1m,
                30 => 2m,
                90 => 4m,
                _ => 6m
            };

            var objetivos = new List<CoordinacionObjetivoDto>
            {
                BuildObjetivo("demanda", $"Demanda {normalizedDays} días", totalSolicitudes, demandMeta, "solicitudes"),
                BuildObjetivo("activacion", "Activación profesional", activacionProfesional, 60m, "%"),
                BuildObjetivo("cumplimiento", "Cierre operativo", cumplimientoOperativo, 35m, "%"),
                BuildObjetivo("chat", "Conversaciones activas", chatsActivos, chatMeta, "chats")
            };

            var alertas = BuildAlertas(objetivos, solicitudesPendientes, solicitudesCompletadas, pagosAprobados, totalProfesionales, pipelinePendiente, slaRespuestaHoras);

            var rubrosTop = solicitudes
                .GroupBy(s => s.Servicio?.Rubro?.Nombre ?? "Sin rubro")
                .Select(group => new CoordinacionRubroDto(
                    group.Key,
                    group.Count(),
                    Math.Round(group.Average(s => s.PresupuestoEstimado), 0),
                    group.Count() == 0 ? 0m : Math.Round(group.Count(s => string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase)) * 100m / group.Count(), 1)))
                .OrderByDescending(item => item.Solicitudes)
                .ThenByDescending(item => item.TicketPromedio)
                .Take(6)
                .ToList();

            var profesionalesTop = solicitudes
                .Where(s => s.ProfesionalId.HasValue && s.Profesional is not null)
                .GroupBy(s => new
                {
                    s.ProfesionalId,
                    Nombre = s.Profesional!.Usuario.Nombre,
                    Rubros = string.Join(", ", s.Profesional.RubrosProfesionales.Select(r => r.Nombre).OrderBy(nombre => nombre))
                })
                .Select(group => new CoordinacionProfesionalDto(
                    group.Key.Nombre,
                    string.IsNullOrWhiteSpace(group.Key.Rubros) ? "Sin rubros" : group.Key.Rubros,
                    group.Count(),
                    group.Count(s => string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase)),
                    group.Where(s => string.Equals(s.Estado, "Completado", StringComparison.OrdinalIgnoreCase))
                        .Sum(s => s.PresupuestoFinal ?? s.PresupuestoEstimado)))
                .OrderByDescending(item => item.TrabajosCompletados)
                .ThenByDescending(item => item.VolumenMovido)
                .Take(6)
                .ToList();

            var movimientos = new List<CoordinacionMovimientoDto>();

            movimientos.AddRange(solicitudes.Take(8).Select(s => new CoordinacionMovimientoDto(
                s.FechaCreacion,
                "Solicitud",
                $"Nueva solicitud: {s.Servicio?.Nombre ?? "Servicio"}",
                $"{s.Cliente?.Usuario?.Nombre ?? "Cliente"} publicó un trabajo en {s.Ubicacion} con estado {s.Estado}.",
                $"{s.PresupuestoEstimado:N0} ARS")));

            var pagosRecientes = await _context.PagosProfesionales.AsNoTracking()
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.FechaCreacion)
                .Take(6)
                .ToListAsync();

            movimientos.AddRange(pagosRecientes.Select(p => new CoordinacionMovimientoDto(
                p.FechaAprobacion ?? p.FechaCreacion,
                "Pago",
                $"Pago {p.Estado}",
                $"{p.Usuario?.Nombre ?? "Usuario"} registró el abono de alta profesional.",
                $"{p.Monto:N0} {p.Moneda}")));

            var mensajesRecientes = await _context.MensajesSolicitud.AsNoTracking()
                .Where(m => !solicitudIds.Any() || solicitudIds.Contains(m.SolicitudTrabajoId))
                .OrderByDescending(m => m.FechaEnvio)
                .Take(6)
                .ToListAsync();

            movimientos.AddRange(mensajesRecientes.Select(m => new CoordinacionMovimientoDto(
                m.FechaEnvio,
                "Chat",
                $"Mensaje en solicitud #{m.SolicitudTrabajoId}",
                $"{m.RemitenteNombre} dejó una actualización en la conversación operativa.",
                m.Contenido.Length > 55 ? $"{m.Contenido[..52]}..." : m.Contenido)));

            movimientos = movimientos
                .OrderByDescending(m => m.Fecha)
                .Take(12)
                .ToList();

            var auditoriaReciente = await _context.AuditoriaEventos
                .AsNoTracking()
                .Include(a => a.Usuario)
                .Where(a => a.Fecha >= recentWindow)
                .OrderByDescending(a => a.Fecha)
                .Take(12)
                .Select(a => new AuditoriaEventoDto(
                    a.Fecha,
                    a.Tipo,
                    a.Accion,
                    a.Descripcion,
                    a.Usuario != null ? a.Usuario.Nombre : "Sistema",
                    a.Entidad,
                    a.EntidadId))
                .ToListAsync();

            var usuariosGestion = adminMode
                ? await BuildAdminUsersAsync()
                : new List<AdminUsuarioGestionDto>();

            return new CoordinacionDashboardDto(
                now,
                normalizedDays,
                string.IsNullOrWhiteSpace(normalizedCity) ? "Todas las ciudades" : normalizedCity,
                rubroName,
                totalUsuarios,
                totalClientes,
                totalProfesionales,
                totalSolicitudes,
                solicitudesPendientes,
                solicitudesAceptadas,
                solicitudesCompletadas,
                solicitudesRechazadas,
                pagosAprobados,
                totalMensajes,
                chatsActivos,
                volumenGestionado,
                pipelinePendiente,
                ticketPromedio,
                cumplimientoOperativo,
                activacionProfesional,
                tasaAceptacion,
                slaRespuestaHoras,
                objetivos,
                alertas,
                rubrosTop,
                profesionalesTop,
                movimientos,
                auditoriaReciente,
                adminMode,
                usuariosGestion);
        }

        private async Task<bool> IsActiveAdminAsync(int? adminUserId)
        {
            if (!adminUserId.HasValue || adminUserId.Value <= 0)
            {
                return false;
            }

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue || claimUserId.Value != adminUserId.Value || !User.IsInRole("Administrador"))
            {
                return false;
            }

            return await _context.Usuarios
                .AsNoTracking()
                .AnyAsync(u => u.Id == adminUserId.Value
                    && u.Activo
                    && u.Rol == "Administrador");
        }

        private async Task<bool> IsActiveSuperAdminAsync(int? adminUserId)
        {
            if (!adminUserId.HasValue || adminUserId.Value <= 0)
            {
                return false;
            }

            var configuredEmail = _configuration["SuperAdmin:Email"]?.Trim();
            if (string.IsNullOrWhiteSpace(configuredEmail))
            {
                return false;
            }

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue || claimUserId.Value != adminUserId.Value || !User.IsInRole("Administrador"))
            {
                return false;
            }

            return await _context.Usuarios
                .AsNoTracking()
                .AnyAsync(u => u.Id == adminUserId.Value
                    && u.Activo
                    && u.Rol == "Administrador"
                    && u.Email.ToLower() == configuredEmail.ToLower());
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private async Task<List<AdminUsuarioGestionDto>> BuildAdminUsersAsync()
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Profesional)
                .Include(u => u.Cliente)
                .OrderByDescending(u => u.FechaRegistro)
                .Take(12)
                .ToListAsync();

            var userIds = usuarios.Select(u => u.Id).ToList();
            var pagos = new List<(int UsuarioId, int PagoId, string Estado)>();

            if (userIds.Count > 0)
            {
                pagos = (await _context.PagosProfesionales
                        .AsNoTracking()
                        .Where(p => userIds.Contains(p.UsuarioId))
                        .OrderByDescending(p => p.FechaCreacion)
                        .Select(p => new { p.UsuarioId, PagoId = p.Id, p.Estado })
                        .ToListAsync())
                    .Select(p => (p.UsuarioId, p.PagoId, p.Estado))
                    .ToList();
            }

            return usuarios.Select(u =>
            {
                var pago = pagos.FirstOrDefault(p => p.UsuarioId == u.Id);
                var tienePago = pago.PagoId > 0;
                var ubicacion = u.Profesional?.Ubicacion ?? u.Cliente?.Ubicacion ?? "Sin ubicación";
                var estadoPago = string.Equals(u.Rol, "Profesional", StringComparison.OrdinalIgnoreCase)
                    ? (tienePago ? pago.Estado : "Sin pago")
                    : "No aplica";

                return new AdminUsuarioGestionDto(
                    u.Id,
                    u.Nombre,
                    u.Email,
                    u.Rol,
                    u.Activo,
                    u.VerificadoRenaper,
                    u.RecibeNotificaciones,
                    u.FechaRegistro,
                    ubicacion,
                    tienePago ? pago.PagoId : null,
                    estadoPago,
                    string.Equals(pago.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase));
            }).ToList();
        }

        private async Task<AdminUsuarioGestionDto?> BuildAdminUserAsync(int userId)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Profesional)
                .Include(u => u.Cliente)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (usuario is null)
            {
                return null;
            }

            var pago = await _context.PagosProfesionales
                .AsNoTracking()
                .Where(p => p.UsuarioId == userId)
                .OrderByDescending(p => p.FechaCreacion)
                .Select(p => new { p.Id, p.Estado })
                .FirstOrDefaultAsync();

            var ubicacion = usuario.Profesional?.Ubicacion ?? usuario.Cliente?.Ubicacion ?? "Sin ubicación";
            var estadoPago = string.Equals(usuario.Rol, "Profesional", StringComparison.OrdinalIgnoreCase)
                ? pago?.Estado ?? "Sin pago"
                : "No aplica";

            return new AdminUsuarioGestionDto(
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                usuario.Rol,
                usuario.Activo,
                usuario.VerificadoRenaper,
                usuario.RecibeNotificaciones,
                usuario.FechaRegistro,
                ubicacion,
                pago?.Id,
                estadoPago,
                string.Equals(pago?.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase));
        }

        private static CoordinacionObjetivoDto BuildObjetivo(string clave, string titulo, decimal actual, decimal meta, string unidad)
        {
            var progreso = meta <= 0 ? 0m : Math.Min(100m, Math.Round(actual * 100m / meta, 1));
            var estado = actual >= meta
                ? "Cumplido"
                : actual >= (meta * 0.75m)
                    ? "En seguimiento"
                    : "Atención";

            var mensaje = estado switch
            {
                "Cumplido" => $"Meta alcanzada: {actual:N1} {unidad} sobre una meta de {meta:N1}.",
                "En seguimiento" => $"Buen ritmo: {actual:N1} {unidad}. Falta poco para llegar a {meta:N1}.",
                _ => $"Necesita foco: {actual:N1} {unidad} frente a la meta de {meta:N1}."
            };

            return new CoordinacionObjetivoDto(clave, titulo, actual, meta, unidad, progreso, estado, mensaje);
        }

        private static List<CoordinacionAlertaDto> BuildAlertas(
            IEnumerable<CoordinacionObjetivoDto> objetivos,
            int solicitudesPendientes,
            int solicitudesCompletadas,
            int pagosAprobados,
            int totalProfesionales,
            decimal pipelinePendiente,
            decimal slaRespuestaHoras)
        {
            var items = new List<CoordinacionAlertaDto>();

            items.AddRange(objetivos.Select(obj => new CoordinacionAlertaDto(
                obj.Estado == "Cumplido" ? "success" : obj.Estado == "En seguimiento" ? "info" : "warning",
                obj.Estado == "Cumplido" ? $"Objetivo logrado: {obj.Titulo}" : $"Seguimiento: {obj.Titulo}",
                obj.Mensaje)));

            if (solicitudesPendientes > solicitudesCompletadas)
            {
                items.Add(new CoordinacionAlertaDto(
                    "warning",
                    "Backlog operativo en revisión",
                    $"Hay {solicitudesPendientes} solicitudes pendientes frente a {solicitudesCompletadas} completadas. Conviene reforzar oferta o tiempos de respuesta."));
            }

            if (totalProfesionales > pagosAprobados)
            {
                items.Add(new CoordinacionAlertaDto(
                    "info",
                    "Activación profesional disponible",
                    $"{totalProfesionales - pagosAprobados} perfiles profesionales todavía pueden convertirse en altas pagas activas."));
            }

            if (pipelinePendiente > 0)
            {
                items.Add(new CoordinacionAlertaDto(
                    "info",
                    "Pipeline económico activo",
                    $"Hay {pipelinePendiente:N0} ARS en solicitudes abiertas o aceptadas todavía por capturar."));
            }

            if (slaRespuestaHoras > 8)
            {
                items.Add(new CoordinacionAlertaDto(
                    "warning",
                    "SLA de respuesta en observación",
                    $"El promedio actual de respuesta está en {slaRespuestaHoras:N1} horas. Conviene acelerar asignaciones."));
            }

            return items.Take(6).ToList();
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var normalized = value.Replace("\"", "\"\"");
            return $"\"{normalized}\"";
        }

        private async Task UnlockIpAsync(AppServicios.Api.Domain.IpBloqueada item, string actor, string? motivo)
        {
            item.Activa = false;
            item.IntentosFallidos = 0;
            item.FechaDesbloqueo = DateTime.UtcNow;
            item.DesbloqueadoPor = actor;

            await _context.SaveChangesAsync();

            await AuditoriaHelper.RegistrarAsync(
                _context,
                null,
                "Seguridad",
                "Desbloqueo IP",
                $"Se desbloqueó la IP {item.Ip}.",
                "IpBloqueada",
                item.Id,
                $"{actor} | {motivo?.Trim()}");
        }

        private static IpBloqueadaDto ToDto(AppServicios.Api.Domain.IpBloqueada item) => new(
            item.Id,
            item.Ip,
            item.Motivo,
            item.IntentosFallidos,
            item.Activa,
            item.FechaCreacion,
            item.FechaUltimoIntento,
            item.FechaDesbloqueo,
            item.DesbloqueadoPor);
    }
}
