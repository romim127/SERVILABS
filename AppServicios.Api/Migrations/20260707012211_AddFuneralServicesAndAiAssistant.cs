using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFuneralServicesAndAiAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Rubros",
                columns: new[] { "Id", "Activo", "Descripcion", "Icono", "Nombre" },
                values: new object[] { 16, true, "Asistencia funeraria, traslados, gestiones y acompañamiento familiar", "servicios-funerarios", "Servicios Funerarios y Acompañamiento" });

            migrationBuilder.InsertData(
                table: "Servicios",
                columns: new[] { "Id", "Activo", "Descripcion", "Nombre", "PrecioSugerido", "RubroId", "Unidad" },
                values: new object[,]
                {
                    { 61, true, "Coordinación de servicio funerario, documentación inicial y orientación familiar.", "Asistencia funeraria integral", 180000m, 16, "por servicio" },
                    { 62, true, "Traslado respetuoso y coordinado según normativa local.", "Traslado funerario", 90000m, 16, "por traslado" },
                    { 63, true, "Gestión de sala, horarios y acompañamiento durante la despedida.", "Sala velatoria", 150000m, 16, "por servicio" },
                    { 64, true, "Coordinación de ceremonia, cementerio y servicio de inhumación.", "Sepelio", 220000m, 16, "por servicio" },
                    { 65, true, "Gestión y coordinación de cremación con acompañamiento familiar.", "Cremación", 240000m, 16, "por servicio" },
                    { 66, true, "Orientación para certificados, permisos y gestiones administrativas.", "Trámites y documentación", 60000m, 16, "por gestión" },
                    { 67, true, "Acompañamiento humano y orientación para familiares en el proceso de duelo.", "Acompañamiento en duelo", 25000m, 16, "por sesión" },
                    { 68, true, "Atención prioritaria para coordinar pasos iniciales ante una pérdida reciente.", "Servicio funerario de urgencia", 200000m, 16, "por urgencia" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "Rubros",
                keyColumn: "Id",
                keyValue: 16);
        }
    }
}
