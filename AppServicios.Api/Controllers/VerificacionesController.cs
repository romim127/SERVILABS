using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.Helpers;
using AppServicios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AppServicios.Api.Controllers;
[ApiController, Authorize, Route("api/Verificaciones")]
public sealed class VerificacionesController(AppServiciosDbContext db, OpenGatewayService gateway) : ControllerBase
{
    private const string CookieName = "servilabs_line_verification";
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private CookieOptions Options => new() { HttpOnly = true, Secure = gateway.CallbackUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true, SameSite = SameSiteMode.Lax, Path = "/api/Verificaciones", MaxAge = TimeSpan.FromMinutes(10) };
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var hash = OpenGatewayService.UserHash(user);
        var results = await db.VerificacionesLinea.AsNoTracking().Where(v => v.UsuarioId == UserId && v.DatosHash == hash && v.Creado > cutoff).OrderByDescending(v => v.Creado).Take(40).ToListAsync();
        return Ok(new { telefono = Telefono.Normalizar(user.Telefono) ?? user.Telefono,
            verificaciones = OpenGatewayService.Types.Select(type => {
                var latest = results.FirstOrDefault(v => v.Tipo == type);
                var state = latest?.Estado;
                if (state is "abrir" or "pendiente" or "procesando" && latest!.Creado < DateTime.UtcNow.AddMinutes(-10)) state = "vencida";
                return new { tipo = type, disponible = gateway.Missing(type).Length == 0, prueba = latest?.Prueba ?? gateway.IsTest(type), estado = state ?? "sin_verificar", fecha = latest?.Completado,
                    verificado = type == "numero" && latest is { Prueba: false, Estado: "coincide" } };
            }) });
    }
    [Authorize(Roles = "Administrador"), HttpGet("configuracion")]
    public IActionResult Configuration() => Ok(OpenGatewayService.Types.Select(type => new { tipo = type, faltantes = gateway.Missing(type), prueba = gateway.IsTest(type) }));
    public sealed class StartRequest
    {
        [Required] public string Tipo { get; set; } = "";
        public bool Consentimiento { get; set; }
        public bool Nativa { get; set; }
        [Range(-90,90)] public double? Latitud { get; set; }
        [Range(-180,180)] public double? Longitud { get; set; }
    }
    [HttpPost("iniciar")]
    public async Task<IActionResult> Start(StartRequest request)
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        if (!OpenGatewayService.Types.Contains(request.Tipo) || !request.Consentimiento) return BadRequest("Elegí una comprobación y autorizá la consulta al operador.");
        if (gateway.Missing(request.Tipo).Length > 0) return StatusCode(503, "Esta comprobación todavía no está disponible. Podés continuar usando tu cuenta.");
        var phone = Telefono.Normalizar(user.Telefono);
        if (phone is null) return BadRequest("Revisá tu celular en Mi cuenta antes de verificarlo.");
        if (request.Tipo == "ubicacion" && (request.Latitud is null || request.Longitud is null)) return BadRequest("Permití acceder a tu ubicación para compararla con la red móvil.");
        var now = DateTime.UtcNow;
        if (await db.VerificacionesLinea.CountAsync(v => v.UsuarioId == UserId && v.Creado > now.AddMinutes(-10)) >= 5) return StatusCode(429, "Esperá unos minutos antes de volver a verificar.");
        await db.VerificacionesLinea.Where(v => v.Creado < now.AddDays(-30)).ExecuteDeleteAsync();
        var state = OpenGatewayService.Random(); var browser = OpenGatewayService.Random();
        var flow = new VerificacionLinea { Id = OpenGatewayService.Hash(state), UsuarioId = UserId, Tipo = request.Tipo,
            Telefono = phone, DatosHash = OpenGatewayService.UserHash(user), NavegadorHash = OpenGatewayService.Hash(browser),
            Verifier = OpenGatewayService.Random(), Prueba = gateway.IsTest(request.Tipo), Nativa = request.Nativa, Estado = request.Nativa ? "abrir" : "pendiente", Latitud = request.Latitud, Longitud = request.Longitud };
        db.Add(flow); await db.SaveChangesAsync();
        if (!request.Nativa) Response.Cookies.Append(CookieName, browser, Options);
        Response.Headers.CacheControl = "no-store";
        var url = request.Nativa
            ? new Uri(new Uri(gateway.CallbackUrl!), "/verificacion-movil.html").AbsoluteUri + "#state=" + state + "&ticket=" + browser
            : gateway.Authorization(flow, state);
        return Ok(new { url, prueba = flow.Prueba });
    }
    public sealed record MobileRequest(string State, string Ticket);
    [AllowAnonymous, HttpPost("abrir")]
    public async Task<IActionResult> OpenMobile(MobileRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(request.State) || request.State.Length > 100 || string.IsNullOrWhiteSpace(request.Ticket) || request.Ticket.Length > 100) return BadRequest("Enlace inválido.");
        var id = OpenGatewayService.Hash(request.State); var ticket = OpenGatewayService.Hash(request.Ticket);
        var flow = await db.VerificacionesLinea.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.Nativa && v.Estado == "abrir" && v.NavegadorHash == ticket && v.Usuario.Activo);
        if (flow is null || flow.Creado < DateTime.UtcNow.AddMinutes(-10)) return BadRequest("El enlace venció o ya fue utilizado. Volvé a la app para intentarlo nuevamente.");
        var browser = OpenGatewayService.Random(); var hash = OpenGatewayService.Hash(browser);
        if (await db.VerificacionesLinea.Where(v => v.Id == id && v.Estado == "abrir").ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, "pendiente").SetProperty(v => v.NavegadorHash, hash)) != 1) return BadRequest("Enlace ya utilizado.");
        Response.Cookies.Append(CookieName, browser, Options);
        return Ok(new { url = gateway.Authorization(flow, request.State) });
    }
    [AllowAnonymous, HttpGet("callback")]
    public async Task<IActionResult> Callback(string? state, string? code, string? error, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.IsNullOrWhiteSpace(state) || state.Length > 100 || !Request.Cookies.TryGetValue(CookieName, out var browser)) return BadRequest("La verificación venció. Volvé a Mi cuenta e intentá nuevamente.");
        var id = OpenGatewayService.Hash(state); var browserHash = OpenGatewayService.Hash(browser);
        var flow = await db.VerificacionesLinea.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.NavegadorHash == browserHash, ct);
        if (flow is null || flow.Estado != "pendiente" || flow.Creado < DateTime.UtcNow.AddMinutes(-10)) return BadRequest("La verificación venció o ya fue utilizada.");
        // Atomically consume state before contacting the provider: replay and concurrent callbacks cannot pass.
        if (await db.VerificacionesLinea.Where(v => v.Id == id && v.Estado == "pendiente").ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, "procesando"), ct) != 1) return BadRequest("Verificación ya utilizada.");
        Response.Cookies.Delete(CookieName, Options);
        var user = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == flow.UsuarioId && u.Activo, ct);
        var outcome = "cancelada";
        if (user is not null && OpenGatewayService.UserHash(user) == flow.DatosHash && string.IsNullOrEmpty(error) && !string.IsNullOrWhiteSpace(code) && code.Length <= 4096)
        {
            try { outcome = await gateway.Complete(flow, user, code, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { outcome = "no_disponible"; }
        }
        await db.VerificacionesLinea.Where(v => v.Id == id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Estado, outcome).SetProperty(v => v.Completado, DateTime.UtcNow).SetProperty(v => v.Verifier, "").SetProperty(v => v.NavegadorHash, "").SetProperty(v => v.Latitud, (double?)null).SetProperty(v => v.Longitud, (double?)null), CancellationToken.None);
        if (flow.Nativa) return LocalRedirect("/verificacion-finalizada.html");
        return LocalRedirect("/?verificacion=finalizada#cuenta");
    }
}
