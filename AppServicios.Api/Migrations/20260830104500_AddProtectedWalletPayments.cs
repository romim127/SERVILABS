using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AppServicios.Api.Migrations
{
    /// <inheritdoc />
    [Migration("20260830104500_AddProtectedWalletPayments")]
    public partial class AddProtectedWalletPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Billeteras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    SaldoDisponible = table.Column<decimal>(type: "numeric", nullable: false),
                    SaldoRetenido = table.Column<decimal>(type: "numeric", nullable: false),
                    Moneda = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Billeteras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Billeteras_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PagosServicioProtegidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolicitudTrabajoId = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    ProfesionalId = table.Column<int>(type: "integer", nullable: false),
                    MontoBruto = table.Column<decimal>(type: "numeric", nullable: false),
                    ComisionPorcentaje = table.Column<decimal>(type: "numeric", nullable: false),
                    ComisionMonto = table.Column<decimal>(type: "numeric", nullable: false),
                    MontoProfesional = table.Column<decimal>(type: "numeric", nullable: false),
                    Moneda = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Proveedor = table.Column<string>(type: "text", nullable: false),
                    ReferenciaExterna = table.Column<string>(type: "text", nullable: false),
                    ReferenciaProveedor = table.Column<string>(type: "text", nullable: false),
                    Detalle = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaPago = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaTrabajoCompletado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaVencimientoLiberacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FechaLiberacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosServicioProtegidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosServicioProtegidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosServicioProtegidos_Profesionales_ProfesionalId",
                        column: x => x.ProfesionalId,
                        principalTable: "Profesionales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PagosServicioProtegidos_SolicitudesTrabajo_SolicitudTrabajoId",
                        column: x => x.SolicitudTrabajoId,
                        principalTable: "SolicitudesTrabajo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisputasPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PagoServicioProtegidoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Resolucion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisputasPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisputasPago_PagosServicioProtegidos_PagoServicioProtegidoId",
                        column: x => x.PagoServicioProtegidoId,
                        principalTable: "PagosServicioProtegidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisputasPago_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosBilletera",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BilleteraId = table.Column<int>(type: "integer", nullable: false),
                    PagoServicioProtegidoId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric", nullable: false),
                    Moneda = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Referencia = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosBilletera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosBilletera_Billeteras_BilleteraId",
                        column: x => x.BilleteraId,
                        principalTable: "Billeteras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimientosBilletera_PagosServicioProtegidos_PagoServicioP~",
                        column: x => x.PagoServicioProtegidoId,
                        principalTable: "PagosServicioProtegidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Billeteras_UsuarioId",
                table: "Billeteras",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisputasPago_PagoServicioProtegidoId_Estado",
                table: "DisputasPago",
                columns: new[] { "PagoServicioProtegidoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputasPago_UsuarioId",
                table: "DisputasPago",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBilletera_BilleteraId_FechaCreacion",
                table: "MovimientosBilletera",
                columns: new[] { "BilleteraId", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBilletera_PagoServicioProtegidoId",
                table: "MovimientosBilletera",
                column: "PagoServicioProtegidoId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosServicioProtegidos_ClienteId",
                table: "PagosServicioProtegidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosServicioProtegidos_Estado",
                table: "PagosServicioProtegidos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_PagosServicioProtegidos_ProfesionalId",
                table: "PagosServicioProtegidos",
                column: "ProfesionalId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosServicioProtegidos_ReferenciaExterna",
                table: "PagosServicioProtegidos",
                column: "ReferenciaExterna",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosServicioProtegidos_SolicitudTrabajoId",
                table: "PagosServicioProtegidos",
                column: "SolicitudTrabajoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DisputasPago");
            migrationBuilder.DropTable(name: "MovimientosBilletera");
            migrationBuilder.DropTable(name: "Billeteras");
            migrationBuilder.DropTable(name: "PagosServicioProtegidos");
        }
    }
}
