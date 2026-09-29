using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppServicios.Api.Data;
using AppServicios.Api.DTOs;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AppServicios.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AuthController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<AppServicios.Api.Domain.Usuario> _passwordHasher;

        public AuthController(
            AppServiciosDbContext context,
            IConfiguration configuration,
            IPasswordHasher<AppServicios.Api.Domain.Usuario> passwordHasher)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        [AllowAnonymous]
        [HttpPost("register-client")]
        public async Task<ActionResult<AuthSessionDto>> RegisterClient([FromBody] RegisterClientRequestDto request)
        {
            var data = request.Usuario;
            var email = data.Email.Trim().ToLowerInvariant();
            var dni = data.Dni.Trim();
            if (data.Rol != "Cliente")
                ModelState.AddModelError(nameof(data.Rol), "Este registro corresponde a una cuenta Cliente.");
            if (string.IsNullOrWhiteSpace(data.PasswordHash))
                ModelState.AddModelError(nameof(data.PasswordHash), "La contraseña es obligatoria.");
            if (data.FechaNacimiento == default || data.FechaNacimiento.Date > DateTime.UtcNow.Date)
                ModelState.AddModelError(nameof(data.FechaNacimiento), "Indica una fecha de nacimiento válida.");
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var usuario = await _context.Usuarios.Include(u => u.Cliente)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (usuario is not null)
            {
                // Resume interrupted registrations only after verifying the owner's credentials.
                if (!usuario.Activo || usuario.Rol != "Cliente" || usuario.DNI != dni
                    || VerifyPassword(usuario, data.PasswordHash!) == PasswordVerificationResult.Failed)
                    return Conflict("Ese email o DNI ya está registrado. Inicia sesión con tu cuenta.");
            }
            else
            {
                if (await _context.Usuarios.AnyAsync(u => u.DNI == dni))
                    return Conflict("Ese email o DNI ya está registrado. Inicia sesión con tu cuenta.");
                usuario = new Domain.Usuario
                {
                    Nombre = data.Nombre.Trim(), Email = email, Telefono = data.Telefono.Trim(),
                    DNI = dni, FechaNacimiento = DateTime.SpecifyKind(data.FechaNacimiento, DateTimeKind.Utc),
                    Rol = "Cliente", Activo = true, VerificadoRenaper = false,
                    RecibeNotificaciones = data.RecibeNotificaciones
                };
                usuario.PasswordHash = _passwordHasher.HashPassword(usuario, data.PasswordHash!);
                _context.Usuarios.Add(usuario);
            }
            if (usuario.Cliente is null)
            {
                usuario.Cliente = new Domain.Cliente
                {
                    Usuario = usuario, Ubicacion = request.Ubicacion.Trim(),
                    Latitud = request.Latitud, Longitud = request.Longitud,
                    Preferencias = request.Preferencias.Trim(), RecibeNotificaciones = data.RecibeNotificaciones
                };
            }
            try
            {
                await _context.SaveChangesAsync();
                var (token, expiresAt) = GenerateJwtToken(usuario);
                var session = await BuildSessionAsync(usuario.Id, token, expiresAt);
                await transaction.CommitAsync();
                return Ok(session);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                return Conflict("Ese email o DNI ya está registrado. Inicia sesión o reintenta con los mismos datos.");
            }
        }

        [Authorize]
        [HttpGet("usuarios/{userId:int}/context")]
        public async Task<ActionResult<AuthSessionDto>> GetContext(int userId)
        {
            if (GetAuthenticatedUserId() != userId && !IsCurrentUserAdmin()) return Forbid();
            var session = await BuildSessionAsync(userId);
            if (session is null) return NotFound();
            if (!session.Activo) return Unauthorized("Tu cuenta está suspendida. Contacta al administrador.");
            return Ok(session);
        }

        private PasswordVerificationResult VerifyPassword(Domain.Usuario usuario, string password)
        {
            try { return _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password); }
            catch (FormatException) { return PasswordVerificationResult.Failed; }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthSessionDto>> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var email = request.Email.Trim();
            var password = request.Password ?? string.Empty;
            var ip = GetRequestIp();
            var userAgent = Request.Headers["User-Agent"].ToString();

            if (await IsIpBlockedAsync(ip))
            {
                await AuditoriaHelper.RegistrarAsync(
                    _context,
                    null,
                    "Seguridad",
                    "IP bloqueada",
                    "Intento de login rechazado por IP bloqueada.",
                    "IpBloqueada",
                    null,
                    $"{ip} | email={email}");

                return StatusCode(StatusCodes.Status429TooManyRequests, "Demasiados intentos fallidos. Contacta al administrador.");
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            var passwordVerification = usuario is null
                ? PasswordVerificationResult.Failed
                : VerifyPassword(usuario, password);
            var legacyPlaintextMatch = usuario is not null
                && passwordVerification == PasswordVerificationResult.Failed
                && string.Equals(usuario.PasswordHash, password, StringComparison.Ordinal);
            var passwordMatches = passwordVerification != PasswordVerificationResult.Failed || legacyPlaintextMatch;
            bool loginExitoso = usuario != null && passwordMatches && usuario.Activo;

            // Registrar sesión
            if (usuario != null)
            {
                if (loginExitoso && (legacyPlaintextMatch || passwordVerification == PasswordVerificationResult.SuccessRehashNeeded))
                {
                    usuario.PasswordHash = _passwordHasher.HashPassword(usuario, password);
                }

                var sesion = new Domain.SesionUsuario
                {
                    UsuarioId = usuario.Id,
                    FechaInicio = DateTime.UtcNow,
                    Ip = ip,
                    UserAgent = userAgent,
                    Exito = loginExitoso,
                    MotivoCierre = loginExitoso ? null : (!usuario.Activo ? "Cuenta suspendida" : "Credenciales incorrectas"),
                    Usuario = usuario
                };
                _context.SesionesUsuario.Add(sesion);
                await _context.SaveChangesAsync();
            }

            if (!passwordMatches && IsSuperAdminEmail(email))
            {
                await RegisterFailedSuperAdminAttemptAsync(ip, email);
            }

            if (usuario is null || !passwordMatches)
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

            if (!usuario.Activo)
            {
                return Unauthorized("Tu cuenta está suspendida. Contacta al administrador.");
            }

            if (IsSuperAdminEmail(usuario.Email))
            {
                await ClearIpFailedAttemptsAsync(ip, "Login correcto de Super Admin");
            }

            var (accessToken, accessTokenExpiresAt) = GenerateJwtToken(usuario);

            var session = await BuildSessionAsync(usuario.Id, accessToken, accessTokenExpiresAt);
            if (session is null)
            {
                return NotFound();
            }

            await AuditoriaHelper.RegistrarAsync(
                _context,
                usuario.Id,
                "Seguridad",
                "Login",
                $"Inicio de sesión correcto para el rol {usuario.Rol}.",
                "Usuario",
                usuario.Id,
                usuario.Email);

            return Ok(session);
        }

        private async Task<AuthSessionDto?> BuildSessionAsync(int userId, string? accessToken = null, DateTime? accessTokenExpiresAt = null)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Cliente)
                .Include(u => u.Profesional!)
                    .ThenInclude(p => p.RubrosProfesionales)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (usuario is null)
            {
                return null;
            }

            var pago = await _context.PagosProfesionales
                .AsNoTracking()
                .Where(p => p.UsuarioId == usuario.Id)
                .OrderByDescending(p => p.FechaCreacion)
                .FirstOrDefaultAsync();

            return new AuthSessionDto(
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                usuario.Rol,
                usuario.Activo,
                usuario.Cliente?.Id,
                usuario.Profesional?.Id,
                usuario.Profesional?.Ubicacion ?? usuario.Cliente?.Ubicacion ?? string.Empty,
                pago?.Id,
                string.Equals(pago?.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase),
                pago?.Estado,
                pago?.Monto,
                pago?.FechaAprobacion,
                usuario.Profesional?.RubrosProfesionales.Select(r => r.Nombre).OrderBy(nombre => nombre).ToList() ?? new List<string>(),
                accessToken,
                accessTokenExpiresAt,
                usuario.IdentidadPresentada && usuario.VerificadoRenaper,
                usuario.IdentidadPresentada);
        }

        private (string Token, DateTime ExpiresAt) GenerateJwtToken(AppServicios.Api.Domain.Usuario usuario)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "AppServicios-Dev-Key-2026-Segura-Preview-32CharsMin";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "AppServicios.Api";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "AppServicios.Client";
            var expiresHours = _configuration.GetValue<int?>("Jwt:ExpiresHours") ?? 8;
            var expiresAt = DateTime.UtcNow.AddHours(Math.Max(1, expiresHours));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, usuario.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Name, usuario.Nombre),
                new(ClaimTypes.Email, usuario.Email),
                new(ClaimTypes.Role, usuario.Rol)
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            var token = new JwtSecurityTokenHandler().WriteToken(jwt);
            return (token, expiresAt);
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private bool IsCurrentUserAdmin() => User.IsInRole("Administrador");

        private bool IsSuperAdminEmail(string email)
        {
            var configured = _configuration["SuperAdmin:Email"]?.Trim();
            return !string.IsNullOrWhiteSpace(configured)
                && string.Equals(email.Trim(), configured, StringComparison.OrdinalIgnoreCase);
        }

        private string GetRequestIp()
        {
            var cfIp = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cfIp))
            {
                return cfIp.Trim();
            }

            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    ?? forwardedFor.Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private async Task<bool> IsIpBlockedAsync(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.Equals(ip, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return await _context.IpsBloqueadas
                .AsNoTracking()
                .AnyAsync(item => item.Ip == ip && item.Activa);
        }

        private async Task RegisterFailedSuperAdminAttemptAsync(string ip, string email)
        {
            if (string.IsNullOrWhiteSpace(ip) || string.Equals(ip, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var threshold = Math.Max(3, _configuration.GetValue<int?>("Security:SuperAdminFailedIpBlockThreshold") ?? 32);
            var item = await _context.IpsBloqueadas.FirstOrDefaultAsync(i => i.Ip == ip);
            if (item is null)
            {
                item = new Domain.IpBloqueada
                {
                    Ip = ip,
                    Motivo = $"Intentos fallidos contra Super Admin ({email}).",
                    IntentosFallidos = 0,
                    Activa = false,
                    FechaCreacion = DateTime.UtcNow
                };
                _context.IpsBloqueadas.Add(item);
            }

            item.IntentosFallidos += 1;
            item.FechaUltimoIntento = DateTime.UtcNow;
            item.Motivo = $"Intentos fallidos contra Super Admin ({email}).";

            if (item.IntentosFallidos >= threshold)
            {
                item.Activa = true;
                item.FechaDesbloqueo = null;
                item.DesbloqueadoPor = string.Empty;
            }

            await _context.SaveChangesAsync();

            if (item.Activa)
            {
                await AuditoriaHelper.RegistrarAsync(
                    _context,
                    null,
                    "Seguridad",
                    "Bloqueo IP",
                    $"Se bloqueó la IP {ip} por {item.IntentosFallidos} intentos fallidos contra Super Admin.",
                    "IpBloqueada",
                    item.Id,
                    item.Motivo);
            }
        }

        private async Task ClearIpFailedAttemptsAsync(string ip, string reason)
        {
            var item = await _context.IpsBloqueadas.FirstOrDefaultAsync(i => i.Ip == ip && !i.Activa);
            if (item is null)
            {
                return;
            }

            item.IntentosFallidos = 0;
            item.FechaDesbloqueo = DateTime.UtcNow;
            item.DesbloqueadoPor = reason;
            await _context.SaveChangesAsync();
        }
    }
}
