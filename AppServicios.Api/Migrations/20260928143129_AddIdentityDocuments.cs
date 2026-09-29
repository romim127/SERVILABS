using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IdentidadPresentada",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "IdentidadDocumentos",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Foto = table.Column<byte[]>(type: "bytea", nullable: false),
                    FotoTipo = table.Column<string>(type: "text", nullable: false),
                    Dni = table.Column<byte[]>(type: "bytea", nullable: false),
                    DniTipo = table.Column<string>(type: "text", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentidadDocumentos", x => x.UsuarioId);
                    table.ForeignKey(
                        name: "FK_IdentidadDocumentos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentidadDocumentos");

            migrationBuilder.DropColumn(
                name: "IdentidadPresentada",
                table: "Usuarios");
        }
    }
}
