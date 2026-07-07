using System.ComponentModel.DataAnnotations;

namespace AppServicios.Api.DTOs
{
    public sealed record UsuarioDto(
        int Id,
        string Nombre,
        string Email,
        string Telefono,
        string Dni,
        DateTime FechaNacimiento,
        string Rol,
        bool Activo,
        DateTime FechaRegistro,
        bool VerificadoRenaper,
        DateTime? FechaVerificacion,
        bool RecibeNotificaciones);

    public sealed class UsuarioUpsertDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 120 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [RegularExpression(@"^[0-9+\-\s]{6,30}$", ErrorMessage = "El teléfono solo puede contener números, espacios, + o -.")]
        public string Telefono { get; set; } = string.Empty;

        [Required(ErrorMessage = "El DNI es obligatorio.")]
        [RegularExpression(@"^\d{7,20}$", ErrorMessage = "El DNI debe contener entre 7 y 20 dígitos.")]
        public string Dni { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
        public DateTime FechaNacimiento { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio.")]
        [RegularExpression("^(Profesional|Cliente|Administrador)$", ErrorMessage = "El rol debe ser Profesional, Cliente o Administrador.")]
        public string Rol { get; set; } = string.Empty;

        [StringLength(300, MinimumLength = 6, ErrorMessage = "La contraseña/hash debe tener entre 6 y 300 caracteres.")]
        public string? PasswordHash { get; set; }

        public bool Activo { get; set; } = true;
        public bool VerificadoRenaper { get; set; }
        public bool RecibeNotificaciones { get; set; } = true;
    }

    public sealed class LoginRequestDto
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(300, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 300 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }

    public sealed record AuthSessionDto(
        int UsuarioId,
        string Nombre,
        string Email,
        string Rol,
        bool Activo,
        int? ClienteId,
        int? ProfesionalId,
        string Ubicacion,
        int? PagoId,
        bool TienePagoAprobado,
        string? EstadoPago,
        decimal? PagoMonto,
        DateTime? FechaPagoAprobacion,
        List<string> Rubros,
        string? AccessToken,
        DateTime? AccessTokenExpiresAt);

    public sealed record ProfesionalDto(
        int Id,
        int UsuarioId,
        string UsuarioNombre,
        string UsuarioEmail,
        double Latitud,
        double Longitud,
        string Ubicacion,
        int AñosExperiencia,
        string Descripcion,
        decimal CalificacionPromedio,
        int TotalTrabajos,
        decimal TarifaBase,
        int RadioAlcanceKm,
        decimal GananciaMensualObjetivo,
        decimal GananciaMensualActual,
        decimal PorcentajeCompletado,
        bool AceptaTrabajoLejano,
        decimal BonoPorDistancia,
        List<int> RubroIds,
        List<string> Rubros);

    public sealed class ProfesionalUpsertDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }

        [Required(ErrorMessage = "La ubicación es obligatoria.")]
        [StringLength(180, MinimumLength = 3, ErrorMessage = "La ubicación debe tener entre 3 y 180 caracteres.")]
        public string Ubicacion { get; set; } = string.Empty;

        [Range(0, 80, ErrorMessage = "Los años de experiencia deben estar entre 0 y 80.")]
        public int AñosExperiencia { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(1200, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 1200 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "La tarifa base debe ser mayor o igual a 0.")]
        public decimal TarifaBase { get; set; }

        [Range(1, 1000, ErrorMessage = "El radio de alcance debe estar entre 1 y 1000 km.")]
        public int RadioAlcanceKm { get; set; } = 10;

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "La meta mensual debe ser mayor o igual a 0.")]
        public decimal GananciaMensualObjetivo { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "La ganancia actual debe ser mayor o igual a 0.")]
        public decimal GananciaMensualActual { get; set; }

        public bool AceptaTrabajoLejano { get; set; } = true;

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El bono por distancia debe ser mayor o igual a 0.")]
        public decimal BonoPorDistancia { get; set; }

        public List<int> RubroIds { get; set; } = new();
    }

    public sealed record PagoProfesionalDto(
        int Id,
        int UsuarioId,
        string UsuarioNombre,
        string UsuarioEmail,
        decimal Monto,
        string Moneda,
        string Concepto,
        string Estado,
        string Proveedor,
        string ReferenciaExterna,
        string Detalle,
        DateTime FechaCreacion,
        DateTime? FechaAprobacion);

    public sealed record MercadoPagoPreferenceDto(
        int PagoId,
        string Estado,
        string PreferenceId,
        string? InitPoint,
        string? SandboxInitPoint,
        string? PublicKey,
        bool SandboxEnabled,
        string Message);

    public sealed record MercadoPagoVerificationDto(
        int PagoId,
        string Estado,
        bool Aprobado,
        string? PaymentProviderStatus,
        string? PaymentProviderId,
        string Message);

    public sealed record PlanComercialDto(
        string Codigo,
        string Nombre,
        string Descripcion,
        decimal AltaMonto,
        decimal MensualidadMonto,
        int MesesPromocion,
        string Moneda,
        int CuposTotales,
        int CuposUsados,
        decimal ComisionPagoProtegidoPorcentaje,
        bool Disponible);

    public sealed class PagoProfesionalCreateDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; } = 2500m;

        [Required(ErrorMessage = "La moneda es obligatoria.")]
        [StringLength(10, MinimumLength = 3, ErrorMessage = "La moneda debe tener entre 3 y 10 caracteres.")]
        public string Moneda { get; set; } = "ARS";

        [Required(ErrorMessage = "El concepto es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El concepto debe tener entre 3 y 120 caracteres.")]
        public string Concepto { get; set; } = "Alta profesional";

        [StringLength(80, ErrorMessage = "El proveedor no puede superar los 80 caracteres.")]
        public string Proveedor { get; set; } = "Checkout Demo AppServicios";

        [StringLength(250, ErrorMessage = "El detalle no puede superar los 250 caracteres.")]
        public string Detalle { get; set; } = string.Empty;

        [StringLength(40, ErrorMessage = "El código de plan no puede superar los 40 caracteres.")]
        public string PlanCodigo { get; set; } = "FUNDADORES_PRO";
    }

    public sealed record NotificacionDto(
        int Id,
        int UsuarioId,
        string Titulo,
        string Mensaje,
        string Tipo,
        int? SolicitudTrabajoId,
        bool Leida,
        DateTime FechaCreacion);

    public sealed record ClienteDto(
        int Id,
        int UsuarioId,
        string UsuarioNombre,
        string UsuarioEmail,
        double Latitud,
        double Longitud,
        string Ubicacion,
        string Preferencias,
        bool RecibeNotificaciones,
        decimal GastoPorMes,
        int TotalServiciosContratados,
        decimal CalificacionPromedioProfesionales);

    public sealed class ClienteUpsertDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }

        [Required(ErrorMessage = "La ubicación es obligatoria.")]
        [StringLength(180, MinimumLength = 3, ErrorMessage = "La ubicación debe tener entre 3 y 180 caracteres.")]
        public string Ubicacion { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Las preferencias no pueden superar los 500 caracteres.")]
        public string Preferencias { get; set; } = string.Empty;

        public bool RecibeNotificaciones { get; set; } = true;
    }

    public sealed record RubroDto(int Id, string Nombre, string Descripcion, string Icono, bool Activo);

    public sealed class RubroUpsertDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(80, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 80 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [StringLength(80, ErrorMessage = "El icono no puede superar los 80 caracteres.")]
        public string Icono { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;
    }

    public sealed record ServicioDto(
        int Id,
        int RubroId,
        string RubroNombre,
        string Nombre,
        string Descripcion,
        decimal PrecioSugerido,
        string Unidad,
        bool Activo);

    public sealed class ServicioUpsertDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un rubro válido.")]
        public int RubroId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(120, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 120 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 1000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El precio sugerido debe ser mayor o igual a 0.")]
        public decimal PrecioSugerido { get; set; }

        [Required(ErrorMessage = "La unidad es obligatoria.")]
        [StringLength(40, MinimumLength = 2, ErrorMessage = "La unidad debe tener entre 2 y 40 caracteres.")]
        public string Unidad { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;
    }

    public sealed record SolicitudTrabajoDto(
        int Id,
        int ClienteId,
        string ClienteNombre,
        int? ProfesionalId,
        string? ProfesionalNombre,
        int ServicioId,
        string ServicioNombre,
        double Latitud,
        double Longitud,
        string Ubicacion,
        string Descripcion,
        DateTime FechaRequerida,
        decimal PresupuestoEstimado,
        decimal? PresupuestoFinal,
        string Estado,
        decimal? DistanciaKm,
        decimal? CostoTraslado,
        decimal? Incentivo,
        DateTime FechaCreacion,
        DateTime? FechaAceptacion,
        DateTime? FechaCompletacion,
        int? CalificacionProfesional,
        string? ComentarioProfesional,
        int? CalificacionCliente,
        string? ComentarioCliente);

    public sealed record MensajeSolicitudDto(
        int Id,
        int SolicitudTrabajoId,
        int UsuarioId,
        string RemitenteNombre,
        string Contenido,
        DateTime FechaEnvio);

    public sealed class MensajeSolicitudCreateDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar una solicitud válida.")]
        public int SolicitudTrabajoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un usuario válido.")]
        public int UsuarioId { get; set; }

        [Required(ErrorMessage = "El mensaje es obligatorio.")]
        [StringLength(1200, MinimumLength = 1, ErrorMessage = "El mensaje debe tener entre 1 y 1200 caracteres.")]
        public string Contenido { get; set; } = string.Empty;
    }

    public sealed class SolicitudTrabajoUpsertDto : IValidatableObject
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar el usuario que opera la solicitud.")]
        public int UsuarioOperadorId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un cliente válido.")]
        public int ClienteId { get; set; }

        public int? ProfesionalId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un servicio válido.")]
        public int ServicioId { get; set; }

        [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
        public double Latitud { get; set; }

        [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
        public double Longitud { get; set; }

        [Required(ErrorMessage = "La ubicación es obligatoria.")]
        [StringLength(180, MinimumLength = 3, ErrorMessage = "La ubicación debe tener entre 3 y 180 caracteres.")]
        public string Ubicacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 2000 caracteres.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha requerida es obligatoria.")]
        public DateTime FechaRequerida { get; set; }

        [Range(typeof(decimal), "0.01", "99999999", ErrorMessage = "El presupuesto estimado debe ser mayor a 0.")]
        public decimal PresupuestoEstimado { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El presupuesto final debe ser mayor o igual a 0.")]
        public decimal? PresupuestoFinal { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio.")]
        [RegularExpression("^(Pendiente|Aceptado|Rechazado|Completado|Cancelado)$", ErrorMessage = "El estado no es válido.")]
        public string Estado { get; set; } = "Pendiente";

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "La distancia debe ser mayor o igual a 0.")]
        public decimal? DistanciaKm { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El costo de traslado debe ser mayor o igual a 0.")]
        public decimal? CostoTraslado { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El incentivo debe ser mayor o igual a 0.")]
        public decimal? Incentivo { get; set; }

        public DateTime? FechaAceptacion { get; set; }
        public DateTime? FechaCompletacion { get; set; }

        [Range(1, 5, ErrorMessage = "La calificación del profesional debe estar entre 1 y 5.")]
        public int? CalificacionProfesional { get; set; }

        [StringLength(500, ErrorMessage = "El comentario del profesional no puede superar los 500 caracteres.")]
        public string? ComentarioProfesional { get; set; }

        [Range(1, 5, ErrorMessage = "La calificación del cliente debe estar entre 1 y 5.")]
        public int? CalificacionCliente { get; set; }

        [StringLength(500, ErrorMessage = "El comentario del cliente no puede superar los 500 caracteres.")]
        public string? ComentarioCliente { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (FechaRequerida.Date < DateTime.UtcNow.Date)
            {
                yield return new ValidationResult(
                    "La fecha requerida no puede ser anterior a hoy.",
                    new[] { nameof(FechaRequerida) });
            }

            if (ProfesionalId.HasValue && ProfesionalId.Value <= 0)
            {
                yield return new ValidationResult(
                    "El profesional indicado no es válido.",
                    new[] { nameof(ProfesionalId) });
            }
        }
    }
}
