using AppServicios.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AppServicios.Api.Controllers;
[ApiController, Route("api/Seguridad")]
public sealed class SeguridadController(IConfiguration config, AppServiciosDbContext db) : ControllerBase
{
    [HttpGet("publica")]
    public IActionResult Publica()
    {
        Response.Headers.CacheControl = "no-store";
        var site = config["Turnstile:SiteKey"];
        var secret = config["Turnstile:SecretKey"];
        return Ok(new { turnstile = new { enabled = !string.IsNullOrWhiteSpace(site) || !string.IsNullOrWhiteSpace(secret) || config.GetValue("Turnstile:Enabled", false),
            ready = !string.IsNullOrWhiteSpace(site) && !string.IsNullOrWhiteSpace(secret), siteKey = site } });
    }
    [Authorize(Roles = "Administrador"), HttpGet("alertas")]
    public async Task<IActionResult> Alertas() => Ok(await db.AuditoriaEventos.AsNoTracking()
        .Where(x => x.Tipo == "Seguridad").OrderByDescending(x => x.Fecha).Take(100)
        .Select(x => new { x.Id, x.Accion, x.Descripcion, x.Fecha }).ToListAsync());
}
