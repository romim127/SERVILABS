using AppServicios.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AppServicios.Api.Data
{
    public class AppServiciosDbContext : DbContext
    {
        public AppServiciosDbContext(DbContextOptions<AppServiciosDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Profesional> Profesionales { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Rubro> Rubros { get; set; }
        public DbSet<Servicio> Servicios { get; set; }
        public DbSet<SolicitudTrabajo> SolicitudesTrabajo { get; set; }
        public DbSet<MensajeSolicitud> MensajesSolicitud { get; set; }
        public DbSet<PagoProfesional> PagosProfesionales { get; set; }
        public DbSet<Notificacion> Notificaciones { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<AuditoriaEvento> AuditoriaEventos { get; set; }
        public DbSet<Certificado> Certificados { get; set; }
        public DbSet<Direccion> Direcciones { get; set; }
        public DbSet<SesionUsuario> SesionesUsuario { get; set; }
        public DbSet<IpBloqueada> IpsBloqueadas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar relaciones
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Profesional)
                .WithOne(p => p.Usuario)
                .HasForeignKey<Profesional>(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Cliente)
                .WithOne(c => c.Usuario)
                .HasForeignKey<Cliente>(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Profesional - Rubro (many-to-many)
            modelBuilder.Entity<Profesional>()
                .HasMany(p => p.RubrosProfesionales)
                .WithMany(r => r.Profesionales)
                .UsingEntity("ProfesionalRubro");

            // Rubro - Servicio
            modelBuilder.Entity<Rubro>()
                .HasMany(r => r.Servicios)
                .WithOne(s => s.Rubro)
                .HasForeignKey(s => s.RubroId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cliente - Direccion
            modelBuilder.Entity<Cliente>()
                .HasMany(c => c.Direcciones)
                .WithOne(d => d.Cliente)
                .HasForeignKey(d => d.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cliente - SolicitudTrabajo
            modelBuilder.Entity<Cliente>()
                .HasMany(c => c.SolicitudesTrabajo)
                .WithOne(s => s.Cliente)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Profesional - SolicitudTrabajo
            modelBuilder.Entity<Profesional>()
                .HasMany(p => p.SolicitudesTrabajo)
                .WithOne(s => s.Profesional)
                .HasForeignKey(s => s.ProfesionalId)
                .OnDelete(DeleteBehavior.SetNull);

            // SolicitudTrabajo - Servicio
            modelBuilder.Entity<SolicitudTrabajo>()
                .HasOne(s => s.Servicio)
                .WithMany(srv => srv.SolicitudesTrabajo)
                .HasForeignKey(s => s.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);

            // MensajeSolicitud - SolicitudTrabajo
            modelBuilder.Entity<MensajeSolicitud>()
                .HasOne(m => m.SolicitudTrabajo)
                .WithMany(s => s.Mensajes)
                .HasForeignKey(m => m.SolicitudTrabajoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MensajeSolicitud>()
                .HasOne(m => m.Usuario)
                .WithMany()
                .HasForeignKey(m => m.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Profesional - Certificado
            modelBuilder.Entity<Profesional>()
                .HasMany(p => p.Certificados)
                .WithOne(c => c.Profesional)
                .HasForeignKey(c => c.ProfesionalId)
                .OnDelete(DeleteBehavior.Cascade);

            // PagoProfesional - Usuario
            modelBuilder.Entity<PagoProfesional>()
                .HasOne(p => p.Usuario)
                .WithMany()
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notificacion - Usuario
            modelBuilder.Entity<Notificacion>()
                .HasOne(n => n.Usuario)
                .WithMany(u => u.Notificaciones)
                .HasForeignKey(n => n.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // SesionUsuario - Usuario
            modelBuilder.Entity<SesionUsuario>()
                .HasOne(s => s.Usuario)
                .WithMany(u => u.SesionesUsuario)
                .HasForeignKey(s => s.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditoriaEvento>()
                .HasOne(a => a.Usuario)
                .WithMany(u => u.AuditoriaEventos)
                .HasForeignKey(a => a.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Usuario>()
                .Property(u => u.RecibeNotificaciones)
                .HasDefaultValue(true);

            // Índices
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.DNI)
                .IsUnique();

            modelBuilder.Entity<PagoProfesional>()
                .HasIndex(p => p.ReferenciaExterna)
                .IsUnique();

            modelBuilder.Entity<Notificacion>()
                .HasIndex(n => n.UsuarioId);

            modelBuilder.Entity<MensajeSolicitud>()
                .HasIndex(m => new { m.SolicitudTrabajoId, m.FechaEnvio });

            modelBuilder.Entity<AuditoriaEvento>()
                .HasIndex(a => new { a.Fecha, a.Tipo });

            modelBuilder.Entity<IpBloqueada>()
                .HasIndex(i => i.Ip)
                .IsUnique();

            // Seed datos iniciales
            modelBuilder.Entity<Rubro>().HasData(
                new Rubro { Id = 1, Nombre = "Electricidad", Descripcion = "Servicios eléctricos", Icono = "electricidad", Activo = true },
                new Rubro { Id = 2, Nombre = "Plomería", Descripcion = "Servicios de plomería", Icono = "plomeria", Activo = true },
                new Rubro { Id = 3, Nombre = "Cuidados de Niños", Descripcion = "Niñeras y cuidadores", Icono = "cuidados-ninos", Activo = true },
                new Rubro { Id = 4, Nombre = "Enfermería", Descripcion = "Cuidados de salud", Icono = "enfermeria", Activo = true },
                new Rubro { Id = 5, Nombre = "Mecánica", Descripcion = "Reparación de vehículos", Icono = "mecanica", Activo = true },
                new Rubro { Id = 6, Nombre = "Metalurgia", Descripcion = "Trabajo en metal", Icono = "metalurgia", Activo = true },
                new Rubro { Id = 7, Nombre = "Soldadura", Descripcion = "Servicios de soldadura", Icono = "soldadura", Activo = true },
                new Rubro { Id = 8, Nombre = "Técnica", Descripcion = "Reparaciones técnicas", Icono = "tecnica", Activo = true },
                new Rubro { Id = 9, Nombre = "Tecnología y Sistemas", Descripcion = "Software, soporte IT, redes y servicios digitales", Icono = "tecnologia-sistemas", Activo = true },
                new Rubro { Id = 10, Nombre = "Salud y Enfermería", Descripcion = "Atención clínica, cuidados y asistencia sanitaria", Icono = "salud-enfermeria", Activo = true },
                new Rubro { Id = 11, Nombre = "Producción, Manufactura y Operarios", Descripcion = "Procesos productivos, planta y operación técnica", Icono = "produccion-manufactura", Activo = true },
                new Rubro { Id = 12, Nombre = "Comercio Ventas y Logística", Descripcion = "Ventas, reparto, logística y atención comercial", Icono = "comercio-logistica", Activo = true },
                new Rubro { Id = 13, Nombre = "Administración Contabilidad y Finanzas", Descripcion = "Gestión administrativa, contable y financiera", Icono = "administracion-finanzas", Activo = true },
                new Rubro { Id = 14, Nombre = "Hostelería Turismo y Gastronomía", Descripcion = "Hotelería, turismo, cocina y atención gastronómica", Icono = "hosteleria-gastronomia", Activo = true },
                new Rubro { Id = 15, Nombre = "Construcción y Servicios Generales", Descripcion = "Obra, mantenimiento general y servicios técnicos", Icono = "construccion-servicios", Activo = true }
            );
        }
    }
}
