using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AppServicios.Api.Controllers;
[ApiController, Authorize, Route("api/Perfiles")]
public sealed class PerfilesController(AppServiciosDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == UserId && u.Activo)) return Unauthorized();
        var user = await db.Usuarios.AsNoTracking().Include(u => u.Profesional).ThenInclude(p => p!.RubrosProfesionales).FirstOrDefaultAsync(u => u.Id == id && u.Activo);
        if (user is null) return NotFound();
        var photo = await db.PerfilesPublicos.Where(p => p.UsuarioId == id).Select(p => (DateTime?)p.Actualizado).FirstOrDefaultAsync();
        return Ok(new { usuarioId = id, nombre = user.Nombre, rol = user.Rol,
            identidadVerificada = user.IdentidadPresentada && user.VerificadoRenaper,
            fotoUrl = photo.HasValue ? $"/api/Perfiles/{id}/foto?v={photo.Value.Ticks}" : null,
            descripcion = user.Profesional?.Descripcion, experiencia = user.Profesional?.AñosExperiencia,
            rubros = user.Profesional?.RubrosProfesionales.Select(r => r.Nombre).ToArray() ?? [] });
    }
    [AllowAnonymous, HttpGet("{id:int}/foto")]
    public async Task<IActionResult> Foto(int id)
    {
        var photo = await db.PerfilesPublicos.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == id && p.Usuario.Activo);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return photo is null ? NotFound() : File(photo.Foto, photo.Tipo);
    }
    [HttpPost("foto"), RequestSizeLimit(4_200_000)]
    public async Task<IActionResult> Upload(IFormFile foto)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == UserId && u.Activo)) return Unauthorized();
        if (foto.Length < 8 || foto.Length > 4_000_000) return BadRequest("Elegí una imagen JPG o PNG de hasta 4 MB.");
        using var memory = new MemoryStream(); await foto.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var type = bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png" : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg" : null;
        if (type is null) return BadRequest("La imagen debe ser JPG o PNG.");
        var profile = await db.PerfilesPublicos.FindAsync(UserId);
        if (profile is null) { profile = new PerfilPublico { UsuarioId = UserId }; db.Add(profile); }
        profile.Foto = bytes; profile.Tipo = type; profile.Actualizado = DateTime.UtcNow;
        await db.SaveChangesAsync(); return await Get(UserId);
    }
    [HttpDelete("foto")]
    public async Task<IActionResult> Delete()
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == UserId && u.Activo)) return Unauthorized();
        await db.PerfilesPublicos.Where(p => p.UsuarioId == UserId).ExecuteDeleteAsync(); return NoContent();
    }
    public sealed record PhoneRequest(string Pais, string Numero);
    [HttpPut("telefono")]
    public async Task<IActionResult> Phone(PhoneRequest request)
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        var phone = Telefono.Normalizar(request.Numero, request.Pais);
        if (phone is null) return BadRequest("Ingresá el código de área y número, sin 0 ni 15 para Argentina.");
        if (user.Telefono != phone)
        {
            user.Telefono = phone;
            await db.VerificacionesLinea.Where(v => v.UsuarioId == UserId).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
        }
        return Ok(new { telefono = phone });
    }
}
