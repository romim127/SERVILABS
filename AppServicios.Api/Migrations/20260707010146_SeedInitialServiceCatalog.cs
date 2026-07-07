using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialServiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Servicios",
                columns: new[] { "Id", "Activo", "Descripcion", "Nombre", "PrecioSugerido", "RubroId", "Unidad" },
                values: new object[,]
                {
                    { 1, true, "Instalaciones, ampliaciones y mejoras eléctricas para viviendas.", "Instalación eléctrica domiciliaria", 35000m, 1, "por trabajo" },
                    { 2, true, "Diagnóstico y reparación de fallas eléctricas urgentes.", "Reparación de cortocircuitos", 30000m, 1, "por visita" },
                    { 3, true, "Armado, reemplazo y revisión de tableros eléctricos.", "Tableros y térmicas", 45000m, 1, "por trabajo" },
                    { 4, true, "Atención rápida para cortes, riesgos o fallas críticas.", "Urgencia eléctrica 24h", 50000m, 1, "por urgencia" },
                    { 5, true, "Detección y arreglo de pérdidas visibles o internas.", "Reparación de pérdidas de agua", 30000m, 2, "por trabajo" },
                    { 6, true, "Destapación de cañerías, piletas, baños y desagües.", "Destapaciones", 35000m, 2, "por trabajo" },
                    { 7, true, "Colocación y reemplazo de griferías, flexibles y accesorios.", "Instalación de grifería", 25000m, 2, "por trabajo" },
                    { 8, true, "Instalación, conexión y revisión de equipos de agua caliente.", "Termotanques y calefones", 55000m, 2, "por trabajo" },
                    { 9, true, "Acompañamiento responsable para niños en domicilio.", "Cuidado de niños por hora", 5000m, 3, "por hora" },
                    { 10, true, "Cuidado extendido con rutinas, comidas y acompañamiento.", "Niñera jornada completa", 30000m, 3, "por jornada" },
                    { 11, true, "Acompañamiento en tareas y hábitos de estudio.", "Apoyo escolar inicial", 8000m, 3, "por hora" },
                    { 12, true, "Servicio ocasional en horarios nocturnos.", "Cuidado eventual nocturno", 12000m, 3, "por hora" },
                    { 13, true, "Acompañamiento básico de salud y bienestar en domicilio.", "Cuidado domiciliario", 12000m, 4, "por hora" },
                    { 14, true, "Toma de presión, temperatura y controles básicos.", "Control de signos vitales", 10000m, 4, "por visita" },
                    { 15, true, "Aplicación segura con indicación médica correspondiente.", "Aplicación de inyectables", 12000m, 4, "por visita" },
                    { 16, true, "Asistencia y seguimiento básico tras una intervención.", "Acompañamiento postoperatorio", 35000m, 4, "por jornada" },
                    { 17, true, "Revisión inicial de fallas y presupuesto de reparación.", "Diagnóstico mecánico", 25000m, 5, "por diagnóstico" },
                    { 18, true, "Cambio de aceite, filtros y revisión general.", "Service básico", 45000m, 5, "por trabajo" },
                    { 19, true, "Revisión y reparación de sistemas de freno y suspensión.", "Frenos y tren delantero", 65000m, 5, "por trabajo" },
                    { 20, true, "Asistencia móvil para fallas o emergencias vehiculares.", "Auxilio mecánico", 40000m, 5, "por salida" },
                    { 21, true, "Trabajo de corte, plegado y preparación de piezas metálicas.", "Cortes y plegados", 40000m, 6, "por trabajo" },
                    { 22, true, "Fabricación y reparación de rejas, portones y estructuras.", "Rejas y estructuras", 90000m, 6, "por trabajo" },
                    { 23, true, "Reparaciones y ajustes en piezas o instalaciones metálicas.", "Mantenimiento metalúrgico", 50000m, 6, "por visita" },
                    { 24, true, "Asesoramiento y fabricación de piezas a medida.", "Diseño de piezas simples", 60000m, 6, "por trabajo" },
                    { 25, true, "Reparaciones de soldadura en hogares y comercios.", "Soldadura domiciliaria", 45000m, 7, "por visita" },
                    { 26, true, "Trabajos estructurales con evaluación técnica previa.", "Soldadura estructural", 90000m, 7, "por trabajo" },
                    { 27, true, "Soldadura, refuerzo y ajuste de portones.", "Reparación de portones", 55000m, 7, "por trabajo" },
                    { 28, true, "Reparación y fabricación de piezas de herrería simple.", "Herrería liviana", 50000m, 7, "por trabajo" },
                    { 29, true, "Diagnóstico y reparación de equipos del hogar.", "Reparación de electrodomésticos", 30000m, 8, "por diagnóstico" },
                    { 30, true, "Instalación, limpieza y mantenimiento de split.", "Aire acondicionado", 55000m, 8, "por equipo" },
                    { 31, true, "Mantenimiento de heladeras, cámaras y freezers comerciales.", "Refrigeración comercial", 65000m, 8, "por visita" },
                    { 32, true, "Reparaciones técnicas variadas para hogares y comercios.", "Soporte técnico general", 30000m, 8, "por visita" },
                    { 33, true, "Reparación, limpieza, instalación y optimización de equipos.", "Soporte PC y notebook", 25000m, 9, "por equipo" },
                    { 34, true, "Instalación y mejora de redes hogareñas o comerciales.", "Redes y WiFi", 35000m, 9, "por trabajo" },
                    { 35, true, "Landing pages, formularios y sitios institucionales.", "Desarrollo web simple", 120000m, 9, "por proyecto" },
                    { 36, true, "Asistencia digital a distancia para problemas de software.", "Soporte remoto", 12000m, 9, "por hora" },
                    { 37, true, "Acompañamiento no médico para adultos mayores.", "Cuidador de adultos", 9000m, 10, "por hora" },
                    { 38, true, "Acompañamiento especializado según necesidad del paciente.", "Acompañante terapéutico", 12000m, 10, "por hora" },
                    { 39, true, "Ayuda en traslados, rutinas y acompañamiento diario.", "Asistencia para movilidad", 10000m, 10, "por hora" },
                    { 40, true, "Acompañamiento nocturno para pacientes o adultos mayores.", "Cuidado nocturno", 35000m, 10, "por noche" },
                    { 41, true, "Personal para tareas productivas, planta o línea.", "Operario de producción", 25000m, 11, "por jornada" },
                    { 42, true, "Tareas de empaque, clasificación y preparación de pedidos.", "Armado y embalaje", 22000m, 11, "por jornada" },
                    { 43, true, "Revisión visual, clasificación y control básico de productos.", "Control de calidad", 28000m, 11, "por jornada" },
                    { 44, true, "Tareas preventivas y correctivas en entornos productivos.", "Mantenimiento de planta", 45000m, 11, "por jornada" },
                    { 45, true, "Atención comercial, ventas y promociones por jornada.", "Vendedor/a eventual", 25000m, 12, "por jornada" },
                    { 46, true, "Entrega de productos, paquetes o documentación.", "Reparto urbano", 18000m, 12, "por jornada" },
                    { 47, true, "Soporte presencial o remoto para comercios y servicios.", "Atención al cliente", 22000m, 12, "por jornada" },
                    { 48, true, "Tareas logísticas, depósito y movimiento de mercadería.", "Carga y descarga", 28000m, 12, "por jornada" },
                    { 49, true, "Carga de datos, gestión documental y apoyo administrativo.", "Administración freelance", 15000m, 13, "por hora" },
                    { 50, true, "Emisión, seguimiento y organización de facturas/cobros.", "Facturación y cobranzas", 18000m, 13, "por hora" },
                    { 51, true, "Soporte contable básico y ordenamiento de comprobantes.", "Asistente contable", 20000m, 13, "por hora" },
                    { 52, true, "Ordenamiento de gastos, ingresos y reportes simples.", "Organización financiera", 25000m, 13, "por hora" },
                    { 53, true, "Atención de mesas para eventos, bares o restaurantes.", "Mozo/a eventual", 25000m, 14, "por jornada" },
                    { 54, true, "Preparación, asistencia y orden en cocina.", "Ayudante de cocina", 28000m, 14, "por jornada" },
                    { 55, true, "Atención de huéspedes, check-in y soporte operativo.", "Recepción hotelera", 30000m, 14, "por jornada" },
                    { 56, true, "Servicio gastronómico para reuniones y eventos.", "Catering para eventos", 90000m, 14, "por evento" },
                    { 57, true, "Reparaciones, ampliaciones y trabajos de obra menor.", "Albañilería general", 50000m, 15, "por jornada" },
                    { 58, true, "Preparación y pintura de ambientes interiores.", "Pintura de interiores", 65000m, 15, "por ambiente" },
                    { 59, true, "Tareas generales de mantenimiento domiciliario o comercial.", "Mantenimiento integral", 45000m, 15, "por visita" },
                    { 60, true, "Equipo coordinado para trabajos de construcción y mantenimiento.", "Cuadrilla de obra", 180000m, 15, "por jornada" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 60);
        }
    }
}
