using System.Security.Claims;
using AppServicios.Api.Data;
using AppServicios.Api.Domain;
using AppServicios.Api.DTOs;
using AppServicios.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class UsuariosController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;
        private readonly IPasswordHasher<Usuario> _passwordHasher;
        private readonly IConfiguration _configuration;

        public UsuariosController(
            AppServiciosDbContext context,
            IPasswordHasher<Usuario> passwordHasher,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetAll()
        {
            var items = await _context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [Authorize(Roles = "Administrador")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UsuarioDto>> GetById(int id)
        {
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return usuario is null ? NotFound() : Ok(ToDto(usuario));
        }

        [HttpPost]
        public async Task<ActionResult<UsuarioDto>> Create([FromBody] UsuarioUpsertDto request)
        {
            await ValidateUsuarioAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var usuario = new Usuario();
            MapToEntity(request, usuario, isCreate: true);

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            await AuditoriaHelper.RegistrarAsync(
                _context,
                usuario.Id,
                "Usuario",
                "Alta",
                $"Se creó el usuario {usuario.Nombre} con rol {usuario.Rol}.",
                "Usuario",
                usuario.Id,
                usuario.Email);

            return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, ToDto(usuario));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<UsuarioDto>> Update(int id, [FromBody] UsuarioUpsertDto request)
        {
            if (!IsCurrentUserSuperAdmin())
            {
                return Unauthorized("Solo el Super Admin puede modificar usuarios.");
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
            if (usuario is null)
            {
                return NotFound();
            }

            await ValidateUsuarioAsync(request, id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            MapToEntity(request, usuario, isCreate: false);
            await _context.SaveChangesAsync();
            await AuditoriaHelper.RegistrarAsync(
                _context,
                usuario.Id,
                "Usuario",
                "Actualización",
                $"Se actualizó el usuario {usuario.Nombre} con rol {usuario.Rol}.",
                "Usuario",
                usuario.Id,
                usuario.Email);

            return Ok(ToDto(usuario));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsCurrentUserSuperAdmin())
            {
                return Unauthorized("Solo el Super Admin puede eliminar usuarios.");
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
            if (usuario is null)
            {
                return NotFound();
            }

            _context.Usuarios.Remove(usuario);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("No se puede eliminar el usuario porque tiene registros relacionados.");
            }
        }

        private async Task ValidateUsuarioAsync(UsuarioUpsertDto request, int? currentId = null)
        {
            var email = request.Email?.Trim() ?? string.Empty;
            var dni = request.Dni?.Trim() ?? string.Empty;

            if (request.FechaNacimiento.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(request.FechaNacimiento), "La fecha de nacimiento no puede estar en el futuro.");
            }

            if (await _context.Usuarios.AnyAsync(u => u.Email == email && (!currentId.HasValue || u.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.Email), "Ya existe un usuario con ese email.");
            }

            if (await _context.Usuarios.AnyAsync(u => u.DNI == dni && (!currentId.HasValue || u.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.Dni), "Ya existe un usuario con ese DNI.");
            }

            if (!currentId.HasValue && string.IsNullOrWhiteSpace(request.PasswordHash))
            {
                ModelState.AddModelError(nameof(request.PasswordHash), "La contraseña/hash es obligatoria al crear un usuario.");
            }

            var requestedRole = request.Rol?.Trim() ?? string.Empty;
            if (!currentId.HasValue && !IsCurrentUserSuperAdmin())
            {
                if (!string.Equals(requestedRole, "Cliente", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(requestedRole, "Profesional", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(request.Rol), "El registro público solo permite crear cuentas Cliente o Profesional.");
                }
            }
        }

        private static UsuarioDto ToDto(Usuario usuario) => new(
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.Telefono,
            usuario.DNI,
            usuario.FechaNacimiento,
            usuario.Rol,
            usuario.Activo,
            usuario.FechaRegistro,
            usuario.VerificadoRenaper,
            usuario.FechaVerificacion,
            usuario.RecibeNotificaciones);

        private void MapToEntity(UsuarioUpsertDto request, Usuario usuario, bool isCreate)
        {
            usuario.Nombre = request.Nombre.Trim();
            usuario.Email = request.Email.Trim();
            usuario.Telefono = request.Telefono.Trim();
            usuario.DNI = request.Dni.Trim();
            usuario.FechaNacimiento = request.FechaNacimiento.Kind == DateTimeKind.Utc
                ? request.FechaNacimiento
                : DateTime.SpecifyKind(request.FechaNacimiento, DateTimeKind.Utc);
            usuario.Rol = request.Rol.Trim();
            usuario.Activo = request.Activo;
            usuario.VerificadoRenaper = request.VerificadoRenaper;
            usuario.RecibeNotificaciones = request.RecibeNotificaciones;

            if (!string.IsNullOrWhiteSpace(request.PasswordHash))
            {
                usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.PasswordHash.Trim());
            }
            else if (isCreate)
            {
                usuario.PasswordHash = string.Empty;
            }
        }

        private bool IsCurrentUserSuperAdmin()
        {
            var configuredEmail = _configuration["SuperAdmin:Email"]?.Trim();
            if (string.IsNullOrWhiteSpace(configuredEmail))
            {
                return false;
            }

            var currentEmail = User.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
            return User.IsInRole("Administrador")
                && string.Equals(currentEmail, configuredEmail, StringComparison.OrdinalIgnoreCase);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class ProfesionalesController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        public ProfesionalesController(AppServiciosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProfesionalDto>>> GetAll()
        {
            var items = await _context.Profesionales
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Include(p => p.RubrosProfesionales)
                .OrderByDescending(p => p.CalificacionPromedio)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProfesionalDto>> GetById(int id)
        {
            var profesional = await _context.Profesionales
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Include(p => p.RubrosProfesionales)
                .FirstOrDefaultAsync(p => p.Id == id);

            return profesional is null ? NotFound() : Ok(ToDto(profesional));
        }

        [HttpPost]
        public async Task<ActionResult<ProfesionalDto>> Create([FromBody] ProfesionalUpsertDto request)
        {
            await ValidateProfesionalAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var profesional = new Profesional();
            MapToEntity(request, profesional);

            var rubros = await GetRubrosAsync(request.RubroIds);
            foreach (var rubro in rubros)
            {
                profesional.RubrosProfesionales.Add(rubro);
            }

            _context.Profesionales.Add(profesional);
            await _context.SaveChangesAsync();

            await _context.Entry(profesional).Reference(p => p.Usuario).LoadAsync();
            await _context.Entry(profesional).Collection(p => p.RubrosProfesionales).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = profesional.Id }, ToDto(profesional));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProfesionalDto>> Update(int id, [FromBody] ProfesionalUpsertDto request)
        {
            var profesional = await _context.Profesionales
                .Include(p => p.Usuario)
                .Include(p => p.RubrosProfesionales)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profesional is null)
            {
                return NotFound();
            }

            await ValidateProfesionalAsync(request, id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            MapToEntity(request, profesional);
            profesional.RubrosProfesionales.Clear();

            var rubros = await GetRubrosAsync(request.RubroIds);
            foreach (var rubro in rubros)
            {
                profesional.RubrosProfesionales.Add(rubro);
            }

            await _context.SaveChangesAsync();
            return Ok(ToDto(profesional));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.Id == id);
            if (profesional is null)
            {
                return NotFound();
            }

            _context.Profesionales.Remove(profesional);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("No se puede eliminar el profesional porque tiene registros relacionados.");
            }
        }

        private async Task ValidateProfesionalAsync(ProfesionalUpsertDto request, int? currentId = null)
        {
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UsuarioId);
            if (usuario is null)
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario seleccionado no existe.");
            }
            else if (!string.Equals(usuario.Rol, "Profesional", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario indicado debe tener rol Profesional.");
            }

            if (await _context.Profesionales.AnyAsync(p => p.UsuarioId == request.UsuarioId && (!currentId.HasValue || p.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "Ese usuario ya está asociado a otro profesional.");
            }

            var rubroIds = request.RubroIds.Where(id => id > 0).Distinct().ToList();
            if (rubroIds.Count != request.RubroIds.Count)
            {
                ModelState.AddModelError(nameof(request.RubroIds), "Todos los rubros deben tener ids válidos.");
            }

            if (rubroIds.Count > 0)
            {
                var existingCount = await _context.Rubros.CountAsync(r => rubroIds.Contains(r.Id));
                if (existingCount != rubroIds.Count)
                {
                    ModelState.AddModelError(nameof(request.RubroIds), "Uno o más rubros no existen.");
                }
            }
        }

        private async Task<List<Rubro>> GetRubrosAsync(IEnumerable<int> rubroIds)
        {
            var ids = rubroIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<Rubro>();
            }

            return await _context.Rubros.Where(r => ids.Contains(r.Id)).ToListAsync();
        }

        private static ProfesionalDto ToDto(Profesional profesional) => new(
            profesional.Id,
            profesional.UsuarioId,
            profesional.Usuario?.Nombre ?? string.Empty,
            profesional.Usuario?.Email ?? string.Empty,
            profesional.Latitud,
            profesional.Longitud,
            profesional.Ubicacion,
            profesional.AñosExperiencia,
            profesional.Descripcion,
            profesional.CalificacionPromedio,
            profesional.TotalTrabajos,
            profesional.TarifaBase,
            profesional.RadioAlcanceKm,
            profesional.GananciaMensualObjetivo,
            profesional.GananciaMensualActual,
            profesional.PorcentajeCompletado,
            profesional.AceptaTrabajoLejano,
            profesional.BonoPorDistancia,
            profesional.RubrosProfesionales.Select(r => r.Id).ToList(),
            profesional.RubrosProfesionales.Select(r => r.Nombre).OrderBy(nombre => nombre).ToList());

        private static void MapToEntity(ProfesionalUpsertDto request, Profesional profesional)
        {
            profesional.UsuarioId = request.UsuarioId;
            profesional.Latitud = request.Latitud;
            profesional.Longitud = request.Longitud;
            profesional.Ubicacion = request.Ubicacion.Trim();
            profesional.AñosExperiencia = request.AñosExperiencia;
            profesional.Descripcion = request.Descripcion.Trim();
            profesional.TarifaBase = request.TarifaBase;
            profesional.RadioAlcanceKm = request.RadioAlcanceKm;
            profesional.GananciaMensualObjetivo = request.GananciaMensualObjetivo;
            profesional.GananciaMensualActual = request.GananciaMensualActual;
            profesional.AceptaTrabajoLejano = request.AceptaTrabajoLejano;
            profesional.BonoPorDistancia = request.BonoPorDistancia;
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class ClientesController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        public ClientesController(AppServiciosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClienteDto>>> GetAll()
        {
            var items = await _context.Clientes
                .AsNoTracking()
                .Include(c => c.Usuario)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ClienteDto>> GetById(int id)
        {
            var cliente = await _context.Clientes
                .AsNoTracking()
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.Id == id);

            return cliente is null ? NotFound() : Ok(ToDto(cliente));
        }

        [HttpPost]
        public async Task<ActionResult<ClienteDto>> Create([FromBody] ClienteUpsertDto request)
        {
            await ValidateClienteAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var cliente = new Cliente();
            MapToEntity(request, cliente);
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            await _context.Entry(cliente).Reference(c => c.Usuario).LoadAsync();
            return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, ToDto(cliente));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ClienteDto>> Update(int id, [FromBody] ClienteUpsertDto request)
        {
            var cliente = await _context.Clientes
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente is null)
            {
                return NotFound();
            }

            await ValidateClienteAsync(request, id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            MapToEntity(request, cliente);
            await _context.SaveChangesAsync();
            return Ok(ToDto(cliente));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
            if (cliente is null)
            {
                return NotFound();
            }

            _context.Clientes.Remove(cliente);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("No se puede eliminar el cliente porque tiene registros relacionados.");
            }
        }

        private async Task ValidateClienteAsync(ClienteUpsertDto request, int? currentId = null)
        {
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UsuarioId);
            if (usuario is null)
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario seleccionado no existe.");
            }
            else if (!string.Equals(usuario.Rol, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "El usuario indicado debe tener rol Cliente.");
            }

            if (await _context.Clientes.AnyAsync(c => c.UsuarioId == request.UsuarioId && (!currentId.HasValue || c.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.UsuarioId), "Ese usuario ya está asociado a otro cliente.");
            }
        }

        private static ClienteDto ToDto(Cliente cliente) => new(
            cliente.Id,
            cliente.UsuarioId,
            cliente.Usuario?.Nombre ?? string.Empty,
            cliente.Usuario?.Email ?? string.Empty,
            cliente.Latitud,
            cliente.Longitud,
            cliente.Ubicacion,
            cliente.Preferencias,
            cliente.RecibeNotificaciones,
            cliente.GastoPorMes,
            cliente.TotalServiciosContratados,
            cliente.CalificacionPromedioProfesionales);

        private static void MapToEntity(ClienteUpsertDto request, Cliente cliente)
        {
            cliente.UsuarioId = request.UsuarioId;
            cliente.Latitud = request.Latitud;
            cliente.Longitud = request.Longitud;
            cliente.Ubicacion = request.Ubicacion.Trim();
            cliente.Preferencias = request.Preferencias.Trim();
            cliente.RecibeNotificaciones = request.RecibeNotificaciones;
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class RubrosController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        public RubrosController(AppServiciosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RubroDto>>> GetAll()
        {
            var items = await _context.Rubros
                .AsNoTracking()
                .OrderBy(r => r.Nombre)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<RubroDto>> GetById(int id)
        {
            var rubro = await _context.Rubros.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            return rubro is null ? NotFound() : Ok(ToDto(rubro));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public async Task<ActionResult<RubroDto>> Create([FromBody] RubroUpsertDto request)
        {
            await ValidateRubroAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var rubro = new Rubro();
            MapToEntity(request, rubro);

            _context.Rubros.Add(rubro);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = rubro.Id }, ToDto(rubro));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<RubroDto>> Update(int id, [FromBody] RubroUpsertDto request)
        {
            var rubro = await _context.Rubros.FirstOrDefaultAsync(r => r.Id == id);
            if (rubro is null)
            {
                return NotFound();
            }

            await ValidateRubroAsync(request, id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            MapToEntity(request, rubro);
            await _context.SaveChangesAsync();

            return Ok(ToDto(rubro));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var rubro = await _context.Rubros.FirstOrDefaultAsync(r => r.Id == id);
            if (rubro is null)
            {
                return NotFound();
            }

            _context.Rubros.Remove(rubro);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("No se puede eliminar el rubro porque tiene servicios o profesionales relacionados.");
            }
        }

        private async Task ValidateRubroAsync(RubroUpsertDto request, int? currentId = null)
        {
            var nombre = request.Nombre?.Trim() ?? string.Empty;
            if (await _context.Rubros.AnyAsync(r => r.Nombre == nombre && (!currentId.HasValue || r.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.Nombre), "Ya existe un rubro con ese nombre.");
            }
        }

        private static RubroDto ToDto(Rubro rubro) => new(rubro.Id, rubro.Nombre, rubro.Descripcion, rubro.Icono, rubro.Activo);

        private static void MapToEntity(RubroUpsertDto request, Rubro rubro)
        {
            rubro.Nombre = request.Nombre.Trim();
            rubro.Descripcion = request.Descripcion.Trim();
            rubro.Icono = request.Icono.Trim();
            rubro.Activo = request.Activo;
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class ServiciosController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        public ServiciosController(AppServiciosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServicioDto>>> GetAll()
        {
            var items = await _context.Servicios
                .AsNoTracking()
                .Include(s => s.Rubro)
                .OrderBy(s => s.Nombre)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ServicioDto>> GetById(int id)
        {
            var servicio = await _context.Servicios
                .AsNoTracking()
                .Include(s => s.Rubro)
                .FirstOrDefaultAsync(s => s.Id == id);

            return servicio is null ? NotFound() : Ok(ToDto(servicio));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public async Task<ActionResult<ServicioDto>> Create([FromBody] ServicioUpsertDto request)
        {
            await ValidateServicioAsync(request);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var servicio = new Servicio();
            MapToEntity(request, servicio);
            _context.Servicios.Add(servicio);
            await _context.SaveChangesAsync();

            await _context.Entry(servicio).Reference(s => s.Rubro).LoadAsync();
            return CreatedAtAction(nameof(GetById), new { id = servicio.Id }, ToDto(servicio));
        }

        [Authorize(Roles = "Administrador")]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ServicioDto>> Update(int id, [FromBody] ServicioUpsertDto request)
        {
            var servicio = await _context.Servicios
                .Include(s => s.Rubro)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (servicio is null)
            {
                return NotFound();
            }

            await ValidateServicioAsync(request, id);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            MapToEntity(request, servicio);
            await _context.SaveChangesAsync();
            await _context.Entry(servicio).Reference(s => s.Rubro).LoadAsync();

            return Ok(ToDto(servicio));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == id);
            if (servicio is null)
            {
                return NotFound();
            }

            _context.Servicios.Remove(servicio);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict("No se puede eliminar el servicio porque tiene solicitudes relacionadas.");
            }
        }

        private async Task ValidateServicioAsync(ServicioUpsertDto request, int? currentId = null)
        {
            if (!await _context.Rubros.AnyAsync(r => r.Id == request.RubroId))
            {
                ModelState.AddModelError(nameof(request.RubroId), "El rubro indicado no existe.");
            }

            var nombre = request.Nombre?.Trim() ?? string.Empty;
            if (await _context.Servicios.AnyAsync(s => s.RubroId == request.RubroId && s.Nombre == nombre && (!currentId.HasValue || s.Id != currentId.Value)))
            {
                ModelState.AddModelError(nameof(request.Nombre), "Ya existe un servicio con ese nombre dentro del rubro seleccionado.");
            }
        }

        private static ServicioDto ToDto(Servicio servicio) => new(
            servicio.Id,
            servicio.RubroId,
            servicio.Rubro?.Nombre ?? string.Empty,
            servicio.Nombre,
            servicio.Descripcion,
            servicio.PrecioSugerido,
            servicio.Unidad,
            servicio.Activo);

        private static void MapToEntity(ServicioUpsertDto request, Servicio servicio)
        {
            servicio.RubroId = request.RubroId;
            servicio.Nombre = request.Nombre.Trim();
            servicio.Descripcion = request.Descripcion.Trim();
            servicio.PrecioSugerido = request.PrecioSugerido;
            servicio.Unidad = request.Unidad.Trim();
            servicio.Activo = request.Activo;
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public sealed class SolicitudesTrabajoController : ControllerBase
    {
        private readonly AppServiciosDbContext _context;

        public SolicitudesTrabajoController(AppServiciosDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SolicitudTrabajoDto>>> GetAll([FromQuery] int? userId = null)
        {
            var query = _context.SolicitudesTrabajo
                .AsNoTracking()
                .Include(s => s.Cliente)
                    .ThenInclude(c => c.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(p => p!.Usuario)
                .Include(s => s.Servicio)
                .AsQueryable();

            if (userId.HasValue && userId.Value > 0)
            {
                var claimUserId = GetAuthenticatedUserId();
                if (!claimUserId.HasValue)
                {
                    return Unauthorized("Debes autenticarte con JWT para consultar tus solicitudes.");
                }

                if (claimUserId.Value != userId.Value && !IsCurrentUserAdmin())
                {
                    return Forbid();
                }

                var actor = await _context.Usuarios
                    .AsNoTracking()
                    .Include(u => u.Cliente)
                    .Include(u => u.Profesional)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (actor is null || !actor.Activo)
                {
                    return Unauthorized("Debes iniciar sesión con una cuenta activa para consultar solicitudes.");
                }

                if (string.Equals(actor.Rol, "Cliente", StringComparison.OrdinalIgnoreCase))
                {
                    var clienteId = actor.Cliente?.Id ?? 0;
                    query = query.Where(s => s.ClienteId == clienteId);
                }
                else if (string.Equals(actor.Rol, "Profesional", StringComparison.OrdinalIgnoreCase))
                {
                    var profesionalId = actor.Profesional?.Id ?? 0;
                    query = query.Where(s => s.Estado == "Pendiente"
                        || (s.ProfesionalId.HasValue && s.ProfesionalId.Value == profesionalId));
                }
            }

            var items = await query
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync();

            return Ok(items.Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SolicitudTrabajoDto>> GetById(int id)
        {
            var solicitud = await _context.SolicitudesTrabajo
                .AsNoTracking()
                .Include(s => s.Cliente)
                    .ThenInclude(c => c!.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(p => p!.Usuario)
                .Include(s => s.Servicio)
                .FirstOrDefaultAsync(s => s.Id == id);

            return solicitud is null ? NotFound() : Ok(ToDto(solicitud));
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<SolicitudTrabajoDto>> Create([FromBody] SolicitudTrabajoUpsertDto request)
        {
            await ApplyAutomaticSolicitudRulesAsync(request, null);
            await ValidateSolicitudAsync(request, null);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var solicitud = new SolicitudTrabajo();
            MapToEntity(request, solicitud, isCreate: true);
            _context.SolicitudesTrabajo.Add(solicitud);
            await SyncProfessionalProgressAsync(solicitud, null, null, 0m);
            await _context.SaveChangesAsync();

            await LoadSolicitudRelationsAsync(solicitud);
            await CreateNotificationsForMatchingProfessionalsAsync(solicitud);
            await AuditoriaHelper.RegistrarAsync(
                _context,
                solicitud.Cliente?.UsuarioId,
                "Solicitud",
                "Alta",
                $"Se creó la solicitud #{solicitud.Id} para {solicitud.Servicio?.Nombre ?? "Servicio"} en {solicitud.Ubicacion}.",
                "SolicitudTrabajo",
                solicitud.Id,
                solicitud.Estado);
            return CreatedAtAction(nameof(GetById), new { id = solicitud.Id }, ToDto(solicitud));
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<SolicitudTrabajoDto>> Update(int id, [FromBody] SolicitudTrabajoUpsertDto request)
        {
            var solicitud = await _context.SolicitudesTrabajo
                .Include(s => s.Cliente)
                    .ThenInclude(c => c!.Usuario)
                .Include(s => s.Profesional)
                    .ThenInclude(p => p!.Usuario)
                .Include(s => s.Servicio)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud is null)
            {
                return NotFound();
            }

            await ApplyAutomaticSolicitudRulesAsync(request, solicitud);
            await ValidateSolicitudAsync(request, solicitud);
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var previousEstado = solicitud.Estado;
            var previousProfesionalId = solicitud.ProfesionalId;
            var previousIngreso = CalculateProfessionalPayout(solicitud);

            MapToEntity(request, solicitud, isCreate: false);
            await SyncProfessionalProgressAsync(solicitud, previousEstado, previousProfesionalId, previousIngreso);
            await _context.SaveChangesAsync();
            await LoadSolicitudRelationsAsync(solicitud);
            await CreateNotificationsForSolicitudStatusChangeAsync(solicitud, previousEstado, previousProfesionalId);
            await AuditoriaHelper.RegistrarAsync(
                _context,
                solicitud.Cliente?.UsuarioId,
                "Solicitud",
                "Actualización",
                $"La solicitud #{solicitud.Id} cambió de {previousEstado} a {solicitud.Estado}.",
                "SolicitudTrabajo",
                solicitud.Id,
                solicitud.Profesional?.Usuario?.Nombre);

            return Ok(ToDto(solicitud));
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var solicitud = await _context.SolicitudesTrabajo.FirstOrDefaultAsync(s => s.Id == id);
            if (solicitud is null)
            {
                return NotFound();
            }

            _context.SolicitudesTrabajo.Remove(solicitud);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task ValidateSolicitudAsync(SolicitudTrabajoUpsertDto request, SolicitudTrabajo? current)
        {
            if (!await _context.Clientes.AnyAsync(c => c.Id == request.ClienteId))
            {
                ModelState.AddModelError(nameof(request.ClienteId), "El cliente indicado no existe.");
            }

            if (!await _context.Servicios.AnyAsync(s => s.Id == request.ServicioId))
            {
                ModelState.AddModelError(nameof(request.ServicioId), "El servicio indicado no existe.");
            }

            if (request.ProfesionalId.HasValue && !await _context.Profesionales.AnyAsync(p => p.Id == request.ProfesionalId.Value))
            {
                ModelState.AddModelError(nameof(request.ProfesionalId), "El profesional indicado no existe.");
            }

            if (string.Equals(request.Estado, "Aceptado", StringComparison.OrdinalIgnoreCase) && !request.ProfesionalId.HasValue)
            {
                ModelState.AddModelError(nameof(request.ProfesionalId), "Debes asignar un profesional si la solicitud está aceptada.");
            }

            var actor = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Cliente)
                .Include(u => u.Profesional)
                .FirstOrDefaultAsync(u => u.Id == request.UsuarioOperadorId);

            var claimUserId = GetAuthenticatedUserId();
            if (!claimUserId.HasValue)
            {
                ModelState.AddModelError(nameof(request.UsuarioOperadorId), "Token inválido o sesión expirada.");
                return;
            }

            if (claimUserId.Value != request.UsuarioOperadorId && !IsCurrentUserAdmin())
            {
                ModelState.AddModelError(nameof(request.UsuarioOperadorId), "El token autenticado no coincide con el usuario operador enviado.");
                return;
            }

            if (actor is null || !actor.Activo)
            {
                ModelState.AddModelError(nameof(request.UsuarioOperadorId), "Debes iniciar sesión con una cuenta activa para operar solicitudes.");
                return;
            }

            if (string.Equals(actor.Rol, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(actor.Rol, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                if (actor.Cliente?.Id != request.ClienteId)
                {
                    ModelState.AddModelError(nameof(request.ClienteId), "Solo puedes operar solicitudes de tu propia cuenta cliente.");
                }

                if (current is null)
                {
                    if (request.ProfesionalId.HasValue)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "Como cliente no puedes asignar un profesional al momento de crear la solicitud.");
                    }

                    if (!string.Equals(request.Estado, "Pendiente", StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(nameof(request.Estado), "La solicitud nueva de un cliente debe comenzar en estado Pendiente.");
                    }

                    return;
                }

                var estadoCliente = request.Estado?.Trim() ?? string.Empty;
                if (!string.Equals(estadoCliente, "Pendiente", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(estadoCliente, "Cancelado", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(request.Estado), "Como cliente solo puedes editar o cancelar tu solicitud. La aceptación o cierre corresponde al profesional o al admin.");
                }

                if (current.ProfesionalId.HasValue && request.ProfesionalId != current.ProfesionalId)
                {
                    ModelState.AddModelError(nameof(request.ProfesionalId), "Como cliente no puedes reasignar manualmente al profesional.");
                }

                return;
            }

            if (string.Equals(actor.Rol, "Profesional", StringComparison.OrdinalIgnoreCase))
            {
                if (current is null)
                {
                    ModelState.AddModelError(nameof(request.UsuarioOperadorId), "Un profesional no puede crear solicitudes como cliente.");
                    return;
                }

                var profesionalId = actor.Profesional?.Id;
                if (!profesionalId.HasValue)
                {
                    ModelState.AddModelError(nameof(request.UsuarioOperadorId), "Tu cuenta profesional no tiene un perfil asociado.");
                    return;
                }

                var estadoProfesional = request.Estado?.Trim() ?? string.Empty;
                if (string.Equals(estadoProfesional, "Aceptado", StringComparison.OrdinalIgnoreCase))
                {
                    if (!request.ProfesionalId.HasValue || request.ProfesionalId.Value != profesionalId.Value)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "Solo puedes aceptarte a ti mismo en una solicitud.");
                    }

                    if (current.ProfesionalId.HasValue && current.ProfesionalId.Value != profesionalId.Value)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "Esta solicitud ya fue tomada por otro profesional.");
                    }
                }
                else if (string.Equals(estadoProfesional, "Rechazado", StringComparison.OrdinalIgnoreCase))
                {
                    if (current.ProfesionalId.HasValue && current.ProfesionalId.Value != profesionalId.Value)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "Solo el profesional involucrado puede rechazar esta solicitud.");
                    }
                }
                else if (string.Equals(estadoProfesional, "Completado", StringComparison.OrdinalIgnoreCase))
                {
                    if (!current.ProfesionalId.HasValue || current.ProfesionalId.Value != profesionalId.Value)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "Solo el profesional asignado puede marcar la solicitud como completada.");
                    }

                    if (!request.ProfesionalId.HasValue || request.ProfesionalId.Value != profesionalId.Value)
                    {
                        ModelState.AddModelError(nameof(request.ProfesionalId), "La solicitud completada debe quedar asignada a tu perfil profesional.");
                    }
                }
                else
                {
                    ModelState.AddModelError(nameof(request.Estado), "Como profesional solo puedes aceptar, rechazar o completar solicitudes.");
                }

                if (request.ClienteId != current.ClienteId || request.ServicioId != current.ServicioId)
                {
                    ModelState.AddModelError(nameof(request.ServicioId), "Como profesional no puedes cambiar el cliente ni el servicio de la solicitud.");
                }

                return;
            }

            ModelState.AddModelError(nameof(request.UsuarioOperadorId), "El rol actual no tiene permisos para operar solicitudes.");
        }

        private async Task ApplyAutomaticSolicitudRulesAsync(SolicitudTrabajoUpsertDto request, SolicitudTrabajo? current)
        {
            request.Estado = string.IsNullOrWhiteSpace(request.Estado)
                ? (current?.Estado ?? "Pendiente")
                : request.Estado.Trim();

            var isAccepted = string.Equals(request.Estado, "Aceptado", StringComparison.OrdinalIgnoreCase);
            var isCompleted = string.Equals(request.Estado, "Completado", StringComparison.OrdinalIgnoreCase);
            var requiresAssignedProfessional = isAccepted || isCompleted;

            if (!request.ProfesionalId.HasValue || request.ProfesionalId.Value <= 0)
            {
                if (current is null)
                {
                    request.DistanciaKm = null;
                    request.CostoTraslado = null;
                    request.Incentivo = null;
                    request.PresupuestoFinal = requiresAssignedProfessional ? request.PresupuestoFinal : null;
                }

                if (!isCompleted)
                {
                    request.FechaCompletacion = null;
                }

                return;
            }

            var profesional = await _context.Profesionales
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProfesionalId.Value);

            if (profesional is null)
            {
                return;
            }

            var resolvedDistance = ResolveDistanceKm(request, current, profesional);
            if (resolvedDistance.HasValue)
            {
                request.DistanciaKm = resolvedDistance.Value;
            }

            if (requiresAssignedProfessional && request.DistanciaKm.HasValue)
            {
                var distanceKm = Math.Max(0m, request.DistanciaKm.Value);
                var extraKm = Math.Max(0m, distanceKm - profesional.RadioAlcanceKm);

                if (extraKm > 0m && !profesional.AceptaTrabajoLejano)
                {
                    ModelState.AddModelError(
                        nameof(request.ProfesionalId),
                        $"La solicitud supera el radio de {profesional.RadioAlcanceKm} km configurado para este profesional. Activa la aceptación de trabajos lejanos para poder continuar.");
                    return;
                }

                var bonusPerKm = Math.Max(0m, profesional.BonoPorDistancia);
                var suggestedTravelCost = Math.Round(distanceKm * bonusPerKm, 2, MidpointRounding.AwayFromZero);
                var suggestedIncentive = extraKm > 0m
                    ? Math.Round(extraKm * bonusPerKm, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                request.CostoTraslado = Math.Max(request.CostoTraslado ?? 0m, suggestedTravelCost);
                request.Incentivo = Math.Max(request.Incentivo ?? 0m, suggestedIncentive);

                var minimumFinalBudget = request.PresupuestoEstimado + (request.CostoTraslado ?? 0m) + (request.Incentivo ?? 0m);
                request.PresupuestoFinal = Math.Max(request.PresupuestoFinal ?? 0m, minimumFinalBudget);
            }

            if (requiresAssignedProfessional)
            {
                request.FechaAceptacion ??= DateTime.UtcNow;
            }

            if (isCompleted)
            {
                request.FechaCompletacion ??= DateTime.UtcNow;
            }
            else
            {
                request.FechaCompletacion = null;
            }
        }

        private async Task SyncProfessionalProgressAsync(SolicitudTrabajo solicitud, string? previousEstado, int? previousProfesionalId, decimal previousIngreso)
        {
            var previousWasCompleted = previousProfesionalId.HasValue
                && string.Equals(previousEstado, "Completado", StringComparison.OrdinalIgnoreCase);

            if (previousWasCompleted && previousProfesionalId.HasValue)
            {
                var previousProfessionalIdValue = previousProfesionalId.GetValueOrDefault();
                var previousProfessional = await _context.Profesionales.FirstOrDefaultAsync(p => p.Id == previousProfessionalIdValue);
                if (previousProfessional is not null)
                {
                    previousProfessional.GananciaMensualActual = Math.Max(0m, previousProfessional.GananciaMensualActual - previousIngreso);
                    previousProfessional.TotalTrabajos = Math.Max(0, previousProfessional.TotalTrabajos - 1);
                }
            }

            var currentIsCompleted = solicitud.ProfesionalId.HasValue
                && string.Equals(solicitud.Estado, "Completado", StringComparison.OrdinalIgnoreCase);

            if (!currentIsCompleted)
            {
                return;
            }

            var currentProfessional = await _context.Profesionales.FirstOrDefaultAsync(p => p.Id == solicitud.ProfesionalId!.Value);
            if (currentProfessional is null)
            {
                return;
            }

            var currentIngreso = CalculateProfessionalPayout(solicitud);
            currentProfessional.GananciaMensualActual += currentIngreso;
            currentProfessional.TotalTrabajos += 1;
        }

        private static decimal CalculateProfessionalPayout(SolicitudTrabajo solicitud)
        {
            if (!string.Equals(solicitud.Estado, "Completado", StringComparison.OrdinalIgnoreCase))
            {
                return 0m;
            }

            return solicitud.PresupuestoFinal
                ?? (solicitud.PresupuestoEstimado + (solicitud.CostoTraslado ?? 0m) + (solicitud.Incentivo ?? 0m));
        }

        private static decimal? ResolveDistanceKm(SolicitudTrabajoUpsertDto request, SolicitudTrabajo? current, Profesional profesional)
        {
            var distances = new List<decimal>();

            if (request.DistanciaKm.HasValue && request.DistanciaKm.Value > 0)
            {
                distances.Add(request.DistanciaKm.Value);
            }

            if (current?.DistanciaKm is decimal currentDistance && currentDistance > 0)
            {
                distances.Add(currentDistance);
            }

            if (IsValidCoordinate(request.Latitud, request.Longitud) && IsValidCoordinate(profesional.Latitud, profesional.Longitud))
            {
                var geoDistance = CalculateDistanceKm(profesional.Latitud, profesional.Longitud, request.Latitud, request.Longitud);
                if (geoDistance > 0)
                {
                    distances.Add(geoDistance);
                }
            }

            return distances.Count > 0 ? distances.Max() : null;
        }

        private static bool IsValidCoordinate(double lat, double lng)
            => Math.Abs(lat) > 0.001
                && Math.Abs(lng) > 0.001
                && lat >= -90 && lat <= 90
                && lng >= -180 && lng <= 180;

        private static decimal CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double earthRadiusKm = 6371d;

            static double ToRadians(double value) => value * Math.PI / 180d;

            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);
            var originLat = ToRadians(lat1);
            var targetLat = ToRadians(lat2);

            var a = Math.Pow(Math.Sin(dLat / 2d), 2d)
                + Math.Cos(originLat) * Math.Cos(targetLat) * Math.Pow(Math.Sin(dLng / 2d), 2d);

            var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
            return Math.Round((decimal)(earthRadiusKm * c), 2, MidpointRounding.AwayFromZero);
        }

        private int? GetAuthenticatedUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }

        private bool IsCurrentUserAdmin() => User.IsInRole("Administrador");

        private async Task LoadSolicitudRelationsAsync(SolicitudTrabajo solicitud)
        {
            await _context.Entry(solicitud).Reference(s => s.Cliente).LoadAsync();
            if (solicitud.Cliente is not null)
            {
                await _context.Entry(solicitud.Cliente).Reference(c => c.Usuario).LoadAsync();
            }

            await _context.Entry(solicitud).Reference(s => s.Servicio).LoadAsync();

            if (solicitud.ProfesionalId.HasValue)
            {
                await _context.Entry(solicitud).Reference(s => s.Profesional).LoadAsync();
                if (solicitud.Profesional is not null)
                {
                    await _context.Entry(solicitud.Profesional).Reference(p => p.Usuario).LoadAsync();
                }
            }
        }

        private async Task CreateNotificationsForMatchingProfessionalsAsync(SolicitudTrabajo solicitud)
        {
            var service = await _context.Servicios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == solicitud.ServicioId);

            if (service is null)
            {
                return;
            }

            var professionalsToNotify = await _context.Profesionales
                .Include(p => p.Usuario)
                .Include(p => p.RubrosProfesionales)
                .Where(p => p.Usuario.RecibeNotificaciones && p.RubrosProfesionales.Any(r => r.Id == service.RubroId))
                .ToListAsync();

            if (professionalsToNotify.Count == 0)
            {
                return;
            }

            var notifications = professionalsToNotify.Select(pro => new Notificacion
            {
                UsuarioId = pro.UsuarioId,
                Titulo = "Nueva solicitud en tu rubro",
                Mensaje = $"Se publicó una nueva solicitud para {service.Nombre} en {solicitud.Ubicacion}. Revisa el panel laboral para responder.",
                Tipo = "SolicitudNueva",
                SolicitudTrabajoId = solicitud.Id,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            }).ToList();

            _context.Notificaciones.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        private async Task CreateNotificationsForSolicitudStatusChangeAsync(SolicitudTrabajo solicitud, string? previousEstado, int? previousProfesionalId)
        {
            if (solicitud.Cliente?.Usuario is null || !solicitud.Cliente.Usuario.RecibeNotificaciones)
            {
                return;
            }

            var currentEstado = solicitud.Estado?.Trim() ?? string.Empty;
            var previous = previousEstado?.Trim() ?? string.Empty;

            if (string.Equals(currentEstado, previous, StringComparison.OrdinalIgnoreCase)
                && previousProfesionalId == solicitud.ProfesionalId)
            {
                return;
            }

            var serviceName = solicitud.Servicio?.Nombre ?? "tu solicitud";
            var professionalName = solicitud.Profesional?.Usuario?.Nombre ?? "un profesional";

            string? titulo = null;
            string? mensaje = null;
            var tipo = "SolicitudActualizada";

            if (string.Equals(currentEstado, "Aceptado", StringComparison.OrdinalIgnoreCase) && solicitud.ProfesionalId.HasValue)
            {
                titulo = "Tu solicitud fue aceptada";
                mensaje = $"{professionalName} aceptó tu solicitud para {serviceName}. Ya puedes coordinar los próximos pasos.";
                tipo = "SolicitudAceptada";
            }
            else if (string.Equals(currentEstado, "Completado", StringComparison.OrdinalIgnoreCase))
            {
                titulo = "Trabajo marcado como completado";
                mensaje = $"{professionalName} indicó que el trabajo de {serviceName} fue completado. Revisa el resultado en tu panel.";
                tipo = "SolicitudCompletada";
            }
            else if (string.Equals(currentEstado, "Rechazado", StringComparison.OrdinalIgnoreCase))
            {
                titulo = "Solicitud rechazada";
                mensaje = $"La solicitud de {serviceName} fue rechazada y quedó disponible para buscar otra opción profesional.";
                tipo = "SolicitudRechazada";
            }

            if (titulo is null || mensaje is null)
            {
                return;
            }

            _context.Notificaciones.Add(new Notificacion
            {
                UsuarioId = solicitud.Cliente.UsuarioId,
                Titulo = titulo,
                Mensaje = mensaje,
                Tipo = tipo,
                SolicitudTrabajoId = solicitud.Id,
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        private static SolicitudTrabajoDto ToDto(SolicitudTrabajo solicitud) => new(
            solicitud.Id,
            solicitud.ClienteId,
            solicitud.Cliente?.Usuario?.Nombre ?? $"Cliente #{solicitud.ClienteId}",
            solicitud.ProfesionalId,
            solicitud.Profesional?.Usuario?.Nombre,
            solicitud.ServicioId,
            solicitud.Servicio?.Nombre ?? string.Empty,
            solicitud.Latitud,
            solicitud.Longitud,
            solicitud.Ubicacion,
            solicitud.Descripcion,
            solicitud.FechaRequerida,
            solicitud.PresupuestoEstimado,
            solicitud.PresupuestoFinal,
            solicitud.Estado,
            solicitud.DistanciaKm,
            solicitud.CostoTraslado,
            solicitud.Incentivo,
            solicitud.FechaCreacion,
            solicitud.FechaAceptacion,
            solicitud.FechaCompletacion,
            solicitud.CalificacionProfesional,
            solicitud.ComentarioProfesional,
            solicitud.CalificacionCliente,
            solicitud.ComentarioCliente);

        private static void MapToEntity(SolicitudTrabajoUpsertDto request, SolicitudTrabajo solicitud, bool isCreate)
        {
            solicitud.ClienteId = request.ClienteId;
            solicitud.ProfesionalId = request.ProfesionalId;
            solicitud.ServicioId = request.ServicioId;
            solicitud.Latitud = request.Latitud;
            solicitud.Longitud = request.Longitud;
            solicitud.Ubicacion = request.Ubicacion.Trim();
            solicitud.Descripcion = request.Descripcion.Trim();
            solicitud.FechaRequerida = EnsureUtc(request.FechaRequerida);
            solicitud.PresupuestoEstimado = request.PresupuestoEstimado;
            solicitud.PresupuestoFinal = request.PresupuestoFinal;
            solicitud.Estado = request.Estado.Trim();
            solicitud.DistanciaKm = request.DistanciaKm;
            solicitud.CostoTraslado = request.CostoTraslado;
            solicitud.Incentivo = request.Incentivo;
            solicitud.FechaAceptacion = request.FechaAceptacion.HasValue ? EnsureUtc(request.FechaAceptacion.Value) : null;
            solicitud.FechaCompletacion = request.FechaCompletacion.HasValue ? EnsureUtc(request.FechaCompletacion.Value) : null;
            solicitud.CalificacionProfesional = request.CalificacionProfesional;
            solicitud.ComentarioProfesional = request.ComentarioProfesional?.Trim() ?? string.Empty;
            solicitud.CalificacionCliente = request.CalificacionCliente;
            solicitud.ComentarioCliente = request.ComentarioCliente?.Trim() ?? string.Empty;

            if (isCreate)
            {
                solicitud.FechaCreacion = DateTime.UtcNow;
            }
        }

        private static DateTime EnsureUtc(DateTime dateTime)
            => dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public sealed class CertificadosController : CrudControllerBase<Certificado>
    {
        public CertificadosController(AppServiciosDbContext context) : base(context) { }
    }

    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public sealed class DireccionesController : CrudControllerBase<Direccion>
    {
        public DireccionesController(AppServiciosDbContext context) : base(context) { }
    }
}
