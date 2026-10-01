using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AppServicios.Api.Controllers;
public abstract class CrudControllerBase<TEntity>(AppServiciosDbContext db) : ControllerBase where TEntity : class
{
    protected DbSet<TEntity> Entities => db.Set<TEntity>();
    int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    bool Admin => User.IsInRole("Administrador");
    IQueryable<TEntity> Owned()
    {
        if (Admin) return Entities;
        if (typeof(TEntity) == typeof(Direccion))
            return (IQueryable<TEntity>)(object)db.Direcciones.Where(x => x.Cliente.UsuarioId == UserId);
        if (typeof(TEntity) == typeof(Certificado))
            return (IQueryable<TEntity>)(object)db.Certificados.Where(x => x.Profesional.UsuarioId == UserId);
        throw new InvalidOperationException("Resource ownership must be defined.");
    }
    async Task<bool> Prepare(TEntity entity)
    {
        if (entity is Direccion d)
        {
            d.Cliente = null!;
            return await db.Clientes.AnyAsync(x => x.Id == d.ClienteId && (Admin || x.UsuarioId == UserId));
        }
        if (entity is Certificado c)
        {
            c.Profesional = null!;
            if (!Admin) c.Verificado = false;
            return await db.Profesionales.AnyAsync(x => x.Id == c.ProfesionalId && (Admin || x.UsuarioId == UserId));
        }
        return false;
    }
    static readonly System.Reflection.PropertyInfo Id = typeof(TEntity).GetProperty("Id")!;
    [HttpGet] public virtual async Task<ActionResult<IEnumerable<TEntity>>> GetAll() => Ok(await Owned().AsNoTracking().ToListAsync());
    [HttpGet("{id:int}")] public virtual async Task<ActionResult<TEntity>> GetById(int id)
    {
        var entity = await Owned().AsNoTracking().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id);
        return entity is null ? NotFound() : Ok(entity);
    }
    [HttpPost] public virtual async Task<ActionResult<TEntity>> Create([FromBody] TEntity entity)
    {
        if (!await Prepare(entity)) return Forbid();
        Id.SetValue(entity, 0);
        Entities.Add(entity);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = Id.GetValue(entity) }, entity);
    }
    [HttpPut("{id:int}")] public virtual async Task<IActionResult> Update(int id, [FromBody] TEntity entity)
    {
        var existing = await Owned().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id);
        if (existing is null) return NotFound();
        if (!await Prepare(entity)) return Forbid();
        Id.SetValue(entity, id);
        db.Entry(existing).CurrentValues.SetValues(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
    [HttpDelete("{id:int}")] public virtual async Task<IActionResult> Delete(int id)
    {
        var existing = await Owned().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id);
        if (existing is null) return NotFound();
        Entities.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
