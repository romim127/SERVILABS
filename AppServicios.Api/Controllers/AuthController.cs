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

        [HttpPost("login")]
        public async Task<ActionResult<AuthSessionDto>> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var email = request.Email.Trim();
            var password = request.Password ?? string.Empty;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == email);

            var passwordVerification = usuario is null
                ? PasswordVerificationResult.Failed
                : _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);
            var legacyPlaintextMatch = usuario is not null
                && passwordVerification == PasswordVerificationResult.Failed
                && string.Equals(usuario.PasswordHash, password, StringComparison.Ordinal);
            var passwordMatches = passwordVerification != PasswordVerificationResult.Failed || legacyPlaintextMatch;
            bool loginExitoso = usuario != null && passwordMatches && usuario.Activo;

            // Captura IP y UserAgent
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();

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

            if (usuario is null || !passwordMatches)
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

            if (!usuario.Activo)
            {
                return Unauthorized("Tu cuenta está suspendida. Contacta al administrador.");
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
                accessTokenExpiresAt);
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
    }
}
