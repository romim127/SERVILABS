using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfilesAndLineVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerfilesPublicos",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Foto = table.Column<byte[]>(type: "bytea", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Actualizado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfilesPublicos", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_PerfilesPublicos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificacionesLinea",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    DatosHash = table.Column<string>(type: "text", nullable: false),
                    NavegadorHash = table.Column<string>(type: "text", nullable: false),
                    Verifier = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Nativa = table.Column<bool>(type: "boolean", nullable: false),
                    Prueba = table.Column<bool>(type: "boolean", nullable: false),
                    Creado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Completado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Latitud = table.Column<double>(type: "double precision", nullable: true),
                    Longitud = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificacionesLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificacionesLinea_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerificacionesLinea_UsuarioId_Creado",
                table: "VerificacionesLinea",
                columns: new[] { "UsuarioId", "Creado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerfilesPublicos");

            migrationBuilder.DropTable(
                name: "VerificacionesLinea");
        }
    }
}
