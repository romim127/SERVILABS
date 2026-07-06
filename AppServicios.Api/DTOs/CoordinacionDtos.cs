using System.ComponentModel.DataAnnotations;

namespace AppServicios.Api.DTOs
{
    public sealed record CoordinacionDashboardDto(
        DateTime FechaActualizacion,
        int PeriodoDias,
        string FiltroCiudad,
        string FiltroRubro,
        int TotalUsuarios,
        int TotalClientes,
        int TotalProfesionales,
        int TotalSolicitudes,
        int SolicitudesPendientes,
        int SolicitudesAceptadas,
        int SolicitudesCompletadas,
        int SolicitudesRechazadas,
        int PagosAprobados,
        int TotalMensajes,
        int ChatsActivos,
        decimal VolumenGestionado,
        decimal PipelinePendiente,
        decimal TicketPromedio,
        decimal CumplimientoOperativo,
        decimal ActivacionProfesional,
        decimal TasaAceptacion,
        decimal SlaRespuestaHoras,
        List<CoordinacionObjetivoDto> Objetivos,
        List<CoordinacionAlertaDto> Alertas,
        List<CoordinacionRubroDto> RubrosTop,
        List<CoordinacionProfesionalDto> ProfesionalesTop,
        List<CoordinacionMovimientoDto> MovimientosRecientes,
        List<AuditoriaEventoDto> AuditoriaReciente,
        bool AdminMode,
        List<AdminUsuarioGestionDto> UsuariosGestion);

    public sealed record CoordinacionObjetivoDto(
        string Clave,
        string Titulo,
        decimal Actual,
        decimal Meta,
        string Unidad,
        decimal Progreso,
        string Estado,
        string Mensaje);

    public sealed record CoordinacionAlertaDto(
        string Tipo,
        string Titulo,
        string Mensaje);

    public sealed record CoordinacionRubroDto(
        string Rubro,
        int Solicitudes,
        decimal TicketPromedio,
        decimal TasaCierre);

    public sealed record CoordinacionProfesionalDto(
        string Profesional,
        string Rubros,
        int TrabajosAsignados,
        int TrabajosCompletados,
        decimal VolumenMovido);

    public sealed record CoordinacionMovimientoDto(
        DateTime Fecha,
        string Tipo,
        string Titulo,
        string Descripcion,
        string Resumen);

    public sealed record AuditoriaEventoDto(
        DateTime Fecha,
        string Tipo,
        string Accion,
        string Descripcion,
        string Usuario,
        string Entidad,
        int? EntidadId);

    public sealed record AdminUsuarioGestionDto(
        int Id,
        string Nombre,
        string Email,
        string Rol,
        bool Activo,
        bool VerificadoRenaper,
        bool RecibeNotificaciones,
        DateTime FechaRegistro,
        string Ubicacion,
        int? PagoId,
        string EstadoPago,
        bool TienePagoAprobado);

    public sealed class AdminUsuarioAccionDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Debes indicar un administrador válido.")]
        public int AdminUserId { get; set; }

        public bool? Activo { get; set; }
        public bool? VerificadoRenaper { get; set; }
        public bool? RecibeNotificaciones { get; set; }

        [StringLength(250, ErrorMessage = "El motivo no puede superar los 250 caracteres.")]
        public string Motivo { get; set; } = string.Empty;
    }

    public sealed record IpBloqueadaDto(
        int Id,
        string Ip,
        string Motivo,
        int IntentosFallidos,
        bool Activa,
        DateTime FechaCreacion,
        DateTime? FechaUltimoIntento,
        DateTime? FechaDesbloqueo,
        string DesbloqueadoPor);

    public sealed class DesbloquearIpDto
    {
        [Required]
        [StringLength(100)]
        public string Ip { get; set; } = string.Empty;

        [StringLength(250)]
        public string Motivo { get; set; } = string.Empty;
    }
}
