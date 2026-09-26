using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGasServiceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Rubros",
                columns: new[] { "Id", "Activo", "Descripcion", "Icono", "Nombre" },
                values: new object[] { 17, true, "Instalaciones y mantenimiento de gas", "gas", "Gas" });

            migrationBuilder.InsertData(
                table: "Servicios",
                columns: new[] { "Id", "Activo", "Descripcion", "Nombre", "PrecioSugerido", "RubroId", "Unidad" },
                values: new object[] { 69, true, "Instalación, revisión y mantenimiento de instalaciones y artefactos a gas.", "Gasista", 0m, 17, "a convenir" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Servicios",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "Rubros",
                keyColumn: "Id",
                keyValue: 17);
        }
    }
}
