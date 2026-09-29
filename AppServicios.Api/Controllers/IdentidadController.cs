using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers;
[ApiController, Authorize, Route("api/Identidad")]
public sealed class IdentidadController(AppServiciosDbContext db) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public sealed class UploadRequest
    {
        [Required] public IFormFile Foto { get; set; } = null!;
        [Required] public IFormFile Dni { get; set; } = null!;
    }
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        return Ok(new { presentada = user.IdentidadPresentada, verificada = user.IdentidadPresentada && user.VerificadoRenaper });
    }
    [HttpPost, RequestSizeLimit(8_500_000)]
    public async Task<IActionResult> Upload([FromForm] UploadRequest request)
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        var photo = await ReadImage(request.Foto);
        var dni = await ReadImage(request.Dni);
        if (photo is null || dni is null) return BadRequest("Adjuntá una foto y una imagen del DNI en JPG o PNG, de hasta 4 MB cada una.");
        var item = await db.IdentidadDocumentos.FindAsync(UserId);
        if (item is null) { item = new IdentidadDocumento { UsuarioId = UserId }; db.Add(item); }
        item.Foto = photo.Value.Bytes; item.FotoTipo = photo.Value.Type;
        item.Dni = dni.Value.Bytes; item.DniTipo = dni.Value.Type;
        item.FechaEnvio = DateTime.UtcNow;
        user.IdentidadPresentada = true;
        user.VerificadoRenaper = false;
        user.FechaVerificacion = null;
        await db.SaveChangesAsync();
        return Ok(new { presentada = true, verificada = false, message = "Foto y DNI enviados. Tu identidad está pendiente de revisión." });
    }
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var user = await db.Usuarios.FindAsync(UserId);
        if (user is null || !user.Activo) return Unauthorized();
        var item = await db.IdentidadDocumentos.FindAsync(UserId);
        if (item is not null) db.Remove(item);
        user.IdentidadPresentada = false; user.VerificadoRenaper = false; user.FechaVerificacion = null;
        await db.SaveChangesAsync();
        return NoContent();
    }
    [HttpGet("{userId:int}/{kind}")]
    public async Task<IActionResult> Image(int userId, string kind)
    {
        if (UserId != userId && !User.IsInRole("Administrador")) return Forbid();
        Response.Headers.CacheControl = "no-store";
        if (kind == "datos") {
            var user = await db.Usuarios.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Nombre, u.DNI, u.FechaNacimiento }).FirstOrDefaultAsync();
            return user is null ? NotFound() : Ok(user);
        }
        if (kind != "foto" && kind != "dni") return NotFound();
        var item = await db.IdentidadDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.UsuarioId == userId);
        if (item is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(kind == "foto" ? item.Foto : item.Dni, kind == "foto" ? item.FotoTipo : item.DniTipo);
    }
    private static async Task<(byte[] Bytes, string Type)?> ReadImage(IFormFile? file)
    {
        if (file is null || file.Length < 8 || file.Length > 4_000_000) return null;
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        var bytes = memory.ToArray();
        if (bytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return (bytes,"image/png");
        if (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return (bytes,"image/jpeg");
        return null;
    }
}
