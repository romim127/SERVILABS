using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class PaymentAndAccessSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CobrosVerificados",
                columns: table => new
                {
                    ProveedorId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Orden = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrosVerificados", x => x.ProveedorId);
                });

            migrationBuilder.CreateTable(
                name: "LimitesAcceso",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Ventana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimitesAcceso", x => x.Clave);
                });

            migrationBuilder.CreateTable(
                name: "TokensRevocados",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Expira = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRevocados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CobrosVerificados_Orden",
                table: "CobrosVerificados",
                column: "Orden",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CobrosVerificados");

            migrationBuilder.DropTable(
                name: "LimitesAcceso");

            migrationBuilder.DropTable(
                name: "TokensRevocados");
        }
    }
}
