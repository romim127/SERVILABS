using System;

namespace AppServicios.Api.Domain
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string DNI { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Rol { get; set; } = string.Empty; // "Profesional" o "Cliente"
        public bool Activo { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
        public string PasswordHash { get; set; } = string.Empty;

        // Verificación de identidad
        public bool IdentidadPresentada { get; set; } = false;
        public bool VerificadoRenaper { get; set; } = false;
        public DateTime? FechaVerificacion { get; set; }

        // Notificaciones
        public bool RecibeNotificaciones { get; set; } = true;

        // Relaciones
        public Profesional? Profesional { get; set; }
        public Cliente? Cliente { get; set; }
        public ICollection<Notificacion> Notificaciones { get; set; } = new List<Notificacion>();
        public ICollection<AuditoriaEvento> AuditoriaEventos { get; set; } = new List<AuditoriaEvento>();
        public ICollection<SesionUsuario> SesionesUsuario { get; set; } = new List<SesionUsuario>();
    }
}
