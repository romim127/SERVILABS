using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace AppServicios.Api.Services;

public sealed class SecurityAlerts(IServiceScopeFactory scopes, ILogger<SecurityAlerts> logger)
{
    public async Task Record(string code, string message)
    {
        logger.LogWarning("Security alert {Code}: {Message}", code, message);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppServiciosDbContext>();
            if (await db.AuditoriaEventos.AnyAsync(x => x.Tipo == "Seguridad" && x.Accion == code && x.Fecha > DateTime.UtcNow.AddMinutes(-10))) return;
            db.AuditoriaEventos.Add(new AuditoriaEvento { Tipo = "Seguridad", Accion = code, Descripcion = message, Entidad = "Seguridad" });
            var admins = await db.Usuarios.Where(x => x.Activo && x.Rol == "Administrador").Select(x => x.Id).ToListAsync();
            foreach (var id in admins) db.Notificaciones.Add(new Notificacion { UsuarioId = id, Tipo = "Seguridad", Titulo = "Revisión de seguridad", Mensaje = message });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogError("Cannot persist security alert {Code}: {Type}", code, ex.GetType().Name); }
    }
}
public sealed class AccessSecurityFilter(AppServiciosDbContext db, IConfiguration config,
    IHttpClientFactory factory, IHostEnvironment environment, SecurityAlerts alerts) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) { await next(); return; }
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        var auth = controller == "Auth" && action is "Login" or "RegisterClient"
            || controller == "Usuarios" && action == "Create" && !http.User.IsInRole("Administrador");
        var scope = auth ? "access" : "write";
        var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        if (auth)
        {
            var arg = context.ActionArguments.Values.FirstOrDefault();
            if (arg is not null)
            {
                var json = JsonSerializer.SerializeToElement(arg);
                if (json.TryGetProperty("Usuario", out var user)) json = user;
                if (json.TryGetProperty("Email", out var email)) subject = email.GetString()?.Trim().ToLowerInvariant() ?? subject;
            }
        }
        var limit = auth ? 20 : http.User.Identity?.IsAuthenticated == true ? 120 : 600;
        var seconds = auth ? 600 : 60;
        if (!await Permit(scope + ":" + subject, seconds, limit))
        {
            http.Response.Headers.RetryAfter = seconds.ToString();
            context.Result = new ObjectResult(new { message = "Hubo demasiados intentos. Esperá unos minutos antes de reintentar." }) { StatusCode = 429 };
            await alerts.Record("Limite de acceso", "Se bloquearon solicitudes repetidas. Revisá la actividad de acceso.");
            return;
        }
        if (auth && (!string.IsNullOrWhiteSpace(config["Turnstile:SiteKey"]) || !string.IsNullOrWhiteSpace(config["Turnstile:SecretKey"]) || config.GetValue("Turnstile:Enabled", false)))
        {
            var token = http.Request.Headers["X-Turnstile-Token"].ToString();
            var expectedAction = action == "Login" ? "login" : "register";
            if (!await ValidateChallenge(token, expectedAction))
            {
                context.Result = new ObjectResult(new { message = "No se pudo validar la verificación de seguridad. Reintentá." }) { StatusCode = 403 };
                await alerts.Record("Verificacion de acceso rechazada", "Se rechazó una verificación de acceso inválida o incompleta.");
                return;
            }
        }
        await next();
    }
    async Task<bool> Permit(string subject, int seconds, int limit)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
        var now = DateTime.UtcNow;
        var window = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / seconds * seconds).UtcDateTime;
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                INSERT INTO "LimitesAcceso" ("Clave", "Ventana", "Intentos") VALUES (@key, @window, 1)
                ON CONFLICT ("Clave") DO UPDATE SET "Intentos" = CASE WHEN "LimitesAcceso"."Ventana" = @window
                    THEN "LimitesAcceso"."Intentos" + 1 ELSE 1 END, "Ventana" = @window
                RETURNING "Intentos"
                """;
            command.Parameters.Add(new NpgsqlParameter("key", key));
            command.Parameters.Add(new NpgsqlParameter("window", window));
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (count == 1) await db.LimitesAcceso.Where(x => x.Ventana < now.AddDays(-1)).ExecuteDeleteAsync();
            return count <= limit;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    async Task<bool> ValidateChallenge(string token, string action)
    {
        if (token.Length is 0 or > 2048 || string.IsNullOrWhiteSpace(config["Turnstile:SecretKey"]) || string.IsNullOrWhiteSpace(config["Turnstile:SiteKey"])) return false;
        var url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        // Local HTTP stub is permitted only in Development, never via production configuration.
        if (environment.IsDevelopment() && Uri.TryCreate(config["Turnstile:TestUrl"], UriKind.Absolute, out var local) && local.IsLoopback) url = local.ToString();
        try
        {
            using var client = factory.CreateClient("Turnstile");
            client.Timeout = TimeSpan.FromSeconds(12);
            using var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
                { ["secret"] = config["Turnstile:SecretKey"]!, ["response"] = token }));
            if (!response.IsSuccessStatusCode) return false;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var value = json.RootElement;
            var hostname = config["Turnstile:Hostname"] ?? "appservicios-mn6i.onrender.com";
            return value.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
                && value.TryGetProperty("hostname", out var host) && host.GetString() == hostname
                && value.TryGetProperty("action", out var operation) && operation.GetString() == action;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) { return false; }
    }
}
