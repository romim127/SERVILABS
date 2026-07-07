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
                new Rubro { Id = 15, Nombre = "Construcción y Servicios Generales", Descripcion = "Obra, mantenimiento general y servicios técnicos", Icono = "construccion-servicios", Activo = true },
                new Rubro { Id = 16, Nombre = "Servicios Funerarios y Acompañamiento", Descripcion = "Asistencia funeraria, traslados, gestiones y acompañamiento familiar", Icono = "servicios-funerarios", Activo = true }
            );

            modelBuilder.Entity<Servicio>().HasData(
                new Servicio { Id = 1, RubroId = 1, Nombre = "Instalación eléctrica domiciliaria", Descripcion = "Instalaciones, ampliaciones y mejoras eléctricas para viviendas.", PrecioSugerido = 35000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 2, RubroId = 1, Nombre = "Reparación de cortocircuitos", Descripcion = "Diagnóstico y reparación de fallas eléctricas urgentes.", PrecioSugerido = 30000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 3, RubroId = 1, Nombre = "Tableros y térmicas", Descripcion = "Armado, reemplazo y revisión de tableros eléctricos.", PrecioSugerido = 45000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 4, RubroId = 1, Nombre = "Urgencia eléctrica 24h", Descripcion = "Atención rápida para cortes, riesgos o fallas críticas.", PrecioSugerido = 50000m, Unidad = "por urgencia", Activo = true },

                new Servicio { Id = 5, RubroId = 2, Nombre = "Reparación de pérdidas de agua", Descripcion = "Detección y arreglo de pérdidas visibles o internas.", PrecioSugerido = 30000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 6, RubroId = 2, Nombre = "Destapaciones", Descripcion = "Destapación de cañerías, piletas, baños y desagües.", PrecioSugerido = 35000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 7, RubroId = 2, Nombre = "Instalación de grifería", Descripcion = "Colocación y reemplazo de griferías, flexibles y accesorios.", PrecioSugerido = 25000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 8, RubroId = 2, Nombre = "Termotanques y calefones", Descripcion = "Instalación, conexión y revisión de equipos de agua caliente.", PrecioSugerido = 55000m, Unidad = "por trabajo", Activo = true },

                new Servicio { Id = 9, RubroId = 3, Nombre = "Cuidado de niños por hora", Descripcion = "Acompañamiento responsable para niños en domicilio.", PrecioSugerido = 5000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 10, RubroId = 3, Nombre = "Niñera jornada completa", Descripcion = "Cuidado extendido con rutinas, comidas y acompañamiento.", PrecioSugerido = 30000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 11, RubroId = 3, Nombre = "Apoyo escolar inicial", Descripcion = "Acompañamiento en tareas y hábitos de estudio.", PrecioSugerido = 8000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 12, RubroId = 3, Nombre = "Cuidado eventual nocturno", Descripcion = "Servicio ocasional en horarios nocturnos.", PrecioSugerido = 12000m, Unidad = "por hora", Activo = true },

                new Servicio { Id = 13, RubroId = 4, Nombre = "Cuidado domiciliario", Descripcion = "Acompañamiento básico de salud y bienestar en domicilio.", PrecioSugerido = 12000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 14, RubroId = 4, Nombre = "Control de signos vitales", Descripcion = "Toma de presión, temperatura y controles básicos.", PrecioSugerido = 10000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 15, RubroId = 4, Nombre = "Aplicación de inyectables", Descripcion = "Aplicación segura con indicación médica correspondiente.", PrecioSugerido = 12000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 16, RubroId = 4, Nombre = "Acompañamiento postoperatorio", Descripcion = "Asistencia y seguimiento básico tras una intervención.", PrecioSugerido = 35000m, Unidad = "por jornada", Activo = true },

                new Servicio { Id = 17, RubroId = 5, Nombre = "Diagnóstico mecánico", Descripcion = "Revisión inicial de fallas y presupuesto de reparación.", PrecioSugerido = 25000m, Unidad = "por diagnóstico", Activo = true },
                new Servicio { Id = 18, RubroId = 5, Nombre = "Service básico", Descripcion = "Cambio de aceite, filtros y revisión general.", PrecioSugerido = 45000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 19, RubroId = 5, Nombre = "Frenos y tren delantero", Descripcion = "Revisión y reparación de sistemas de freno y suspensión.", PrecioSugerido = 65000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 20, RubroId = 5, Nombre = "Auxilio mecánico", Descripcion = "Asistencia móvil para fallas o emergencias vehiculares.", PrecioSugerido = 40000m, Unidad = "por salida", Activo = true },

                new Servicio { Id = 21, RubroId = 6, Nombre = "Cortes y plegados", Descripcion = "Trabajo de corte, plegado y preparación de piezas metálicas.", PrecioSugerido = 40000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 22, RubroId = 6, Nombre = "Rejas y estructuras", Descripcion = "Fabricación y reparación de rejas, portones y estructuras.", PrecioSugerido = 90000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 23, RubroId = 6, Nombre = "Mantenimiento metalúrgico", Descripcion = "Reparaciones y ajustes en piezas o instalaciones metálicas.", PrecioSugerido = 50000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 24, RubroId = 6, Nombre = "Diseño de piezas simples", Descripcion = "Asesoramiento y fabricación de piezas a medida.", PrecioSugerido = 60000m, Unidad = "por trabajo", Activo = true },

                new Servicio { Id = 25, RubroId = 7, Nombre = "Soldadura domiciliaria", Descripcion = "Reparaciones de soldadura en hogares y comercios.", PrecioSugerido = 45000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 26, RubroId = 7, Nombre = "Soldadura estructural", Descripcion = "Trabajos estructurales con evaluación técnica previa.", PrecioSugerido = 90000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 27, RubroId = 7, Nombre = "Reparación de portones", Descripcion = "Soldadura, refuerzo y ajuste de portones.", PrecioSugerido = 55000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 28, RubroId = 7, Nombre = "Herrería liviana", Descripcion = "Reparación y fabricación de piezas de herrería simple.", PrecioSugerido = 50000m, Unidad = "por trabajo", Activo = true },

                new Servicio { Id = 29, RubroId = 8, Nombre = "Reparación de electrodomésticos", Descripcion = "Diagnóstico y reparación de equipos del hogar.", PrecioSugerido = 30000m, Unidad = "por diagnóstico", Activo = true },
                new Servicio { Id = 30, RubroId = 8, Nombre = "Aire acondicionado", Descripcion = "Instalación, limpieza y mantenimiento de split.", PrecioSugerido = 55000m, Unidad = "por equipo", Activo = true },
                new Servicio { Id = 31, RubroId = 8, Nombre = "Refrigeración comercial", Descripcion = "Mantenimiento de heladeras, cámaras y freezers comerciales.", PrecioSugerido = 65000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 32, RubroId = 8, Nombre = "Soporte técnico general", Descripcion = "Reparaciones técnicas variadas para hogares y comercios.", PrecioSugerido = 30000m, Unidad = "por visita", Activo = true },

                new Servicio { Id = 33, RubroId = 9, Nombre = "Soporte PC y notebook", Descripcion = "Reparación, limpieza, instalación y optimización de equipos.", PrecioSugerido = 25000m, Unidad = "por equipo", Activo = true },
                new Servicio { Id = 34, RubroId = 9, Nombre = "Redes y WiFi", Descripcion = "Instalación y mejora de redes hogareñas o comerciales.", PrecioSugerido = 35000m, Unidad = "por trabajo", Activo = true },
                new Servicio { Id = 35, RubroId = 9, Nombre = "Desarrollo web simple", Descripcion = "Landing pages, formularios y sitios institucionales.", PrecioSugerido = 120000m, Unidad = "por proyecto", Activo = true },
                new Servicio { Id = 36, RubroId = 9, Nombre = "Soporte remoto", Descripcion = "Asistencia digital a distancia para problemas de software.", PrecioSugerido = 12000m, Unidad = "por hora", Activo = true },

                new Servicio { Id = 37, RubroId = 10, Nombre = "Cuidador de adultos", Descripcion = "Acompañamiento no médico para adultos mayores.", PrecioSugerido = 9000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 38, RubroId = 10, Nombre = "Acompañante terapéutico", Descripcion = "Acompañamiento especializado según necesidad del paciente.", PrecioSugerido = 12000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 39, RubroId = 10, Nombre = "Asistencia para movilidad", Descripcion = "Ayuda en traslados, rutinas y acompañamiento diario.", PrecioSugerido = 10000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 40, RubroId = 10, Nombre = "Cuidado nocturno", Descripcion = "Acompañamiento nocturno para pacientes o adultos mayores.", PrecioSugerido = 35000m, Unidad = "por noche", Activo = true },

                new Servicio { Id = 41, RubroId = 11, Nombre = "Operario de producción", Descripcion = "Personal para tareas productivas, planta o línea.", PrecioSugerido = 25000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 42, RubroId = 11, Nombre = "Armado y embalaje", Descripcion = "Tareas de empaque, clasificación y preparación de pedidos.", PrecioSugerido = 22000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 43, RubroId = 11, Nombre = "Control de calidad", Descripcion = "Revisión visual, clasificación y control básico de productos.", PrecioSugerido = 28000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 44, RubroId = 11, Nombre = "Mantenimiento de planta", Descripcion = "Tareas preventivas y correctivas en entornos productivos.", PrecioSugerido = 45000m, Unidad = "por jornada", Activo = true },

                new Servicio { Id = 45, RubroId = 12, Nombre = "Vendedor/a eventual", Descripcion = "Atención comercial, ventas y promociones por jornada.", PrecioSugerido = 25000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 46, RubroId = 12, Nombre = "Reparto urbano", Descripcion = "Entrega de productos, paquetes o documentación.", PrecioSugerido = 18000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 47, RubroId = 12, Nombre = "Atención al cliente", Descripcion = "Soporte presencial o remoto para comercios y servicios.", PrecioSugerido = 22000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 48, RubroId = 12, Nombre = "Carga y descarga", Descripcion = "Tareas logísticas, depósito y movimiento de mercadería.", PrecioSugerido = 28000m, Unidad = "por jornada", Activo = true },

                new Servicio { Id = 49, RubroId = 13, Nombre = "Administración freelance", Descripcion = "Carga de datos, gestión documental y apoyo administrativo.", PrecioSugerido = 15000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 50, RubroId = 13, Nombre = "Facturación y cobranzas", Descripcion = "Emisión, seguimiento y organización de facturas/cobros.", PrecioSugerido = 18000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 51, RubroId = 13, Nombre = "Asistente contable", Descripcion = "Soporte contable básico y ordenamiento de comprobantes.", PrecioSugerido = 20000m, Unidad = "por hora", Activo = true },
                new Servicio { Id = 52, RubroId = 13, Nombre = "Organización financiera", Descripcion = "Ordenamiento de gastos, ingresos y reportes simples.", PrecioSugerido = 25000m, Unidad = "por hora", Activo = true },

                new Servicio { Id = 53, RubroId = 14, Nombre = "Mozo/a eventual", Descripcion = "Atención de mesas para eventos, bares o restaurantes.", PrecioSugerido = 25000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 54, RubroId = 14, Nombre = "Ayudante de cocina", Descripcion = "Preparación, asistencia y orden en cocina.", PrecioSugerido = 28000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 55, RubroId = 14, Nombre = "Recepción hotelera", Descripcion = "Atención de huéspedes, check-in y soporte operativo.", PrecioSugerido = 30000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 56, RubroId = 14, Nombre = "Catering para eventos", Descripcion = "Servicio gastronómico para reuniones y eventos.", PrecioSugerido = 90000m, Unidad = "por evento", Activo = true },

                new Servicio { Id = 57, RubroId = 15, Nombre = "Albañilería general", Descripcion = "Reparaciones, ampliaciones y trabajos de obra menor.", PrecioSugerido = 50000m, Unidad = "por jornada", Activo = true },
                new Servicio { Id = 58, RubroId = 15, Nombre = "Pintura de interiores", Descripcion = "Preparación y pintura de ambientes interiores.", PrecioSugerido = 65000m, Unidad = "por ambiente", Activo = true },
                new Servicio { Id = 59, RubroId = 15, Nombre = "Mantenimiento integral", Descripcion = "Tareas generales de mantenimiento domiciliario o comercial.", PrecioSugerido = 45000m, Unidad = "por visita", Activo = true },
                new Servicio { Id = 60, RubroId = 15, Nombre = "Cuadrilla de obra", Descripcion = "Equipo coordinado para trabajos de construcción y mantenimiento.", PrecioSugerido = 180000m, Unidad = "por jornada", Activo = true },

                new Servicio { Id = 61, RubroId = 16, Nombre = "Asistencia funeraria integral", Descripcion = "Coordinación de servicio funerario, documentación inicial y orientación familiar.", PrecioSugerido = 180000m, Unidad = "por servicio", Activo = true },
                new Servicio { Id = 62, RubroId = 16, Nombre = "Traslado funerario", Descripcion = "Traslado respetuoso y coordinado según normativa local.", PrecioSugerido = 90000m, Unidad = "por traslado", Activo = true },
                new Servicio { Id = 63, RubroId = 16, Nombre = "Sala velatoria", Descripcion = "Gestión de sala, horarios y acompañamiento durante la despedida.", PrecioSugerido = 150000m, Unidad = "por servicio", Activo = true },
                new Servicio { Id = 64, RubroId = 16, Nombre = "Sepelio", Descripcion = "Coordinación de ceremonia, cementerio y servicio de inhumación.", PrecioSugerido = 220000m, Unidad = "por servicio", Activo = true },
                new Servicio { Id = 65, RubroId = 16, Nombre = "Cremación", Descripcion = "Gestión y coordinación de cremación con acompañamiento familiar.", PrecioSugerido = 240000m, Unidad = "por servicio", Activo = true },
                new Servicio { Id = 66, RubroId = 16, Nombre = "Trámites y documentación", Descripcion = "Orientación para certificados, permisos y gestiones administrativas.", PrecioSugerido = 60000m, Unidad = "por gestión", Activo = true },
                new Servicio { Id = 67, RubroId = 16, Nombre = "Acompañamiento en duelo", Descripcion = "Acompañamiento humano y orientación para familiares en el proceso de duelo.", PrecioSugerido = 25000m, Unidad = "por sesión", Activo = true },
                new Servicio { Id = 68, RubroId = 16, Nombre = "Servicio funerario de urgencia", Descripcion = "Atención prioritaria para coordinar pasos iniciales ante una pérdida reciente.", PrecioSugerido = 200000m, Unidad = "por urgencia", Activo = true }
            );
        }
    }
}
