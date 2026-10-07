using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FirmaManuscrita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirmaAprobadorId",
                table: "Solicitud",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmaSolicitanteId",
                table: "Solicitud",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FirmaUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Datos = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaUsuario_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_FirmaAprobadorId",
                table: "Solicitud",
                column: "FirmaAprobadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_FirmaSolicitanteId",
                table: "Solicitud",
                column: "FirmaSolicitanteId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaUsuario_UsuarioId",
                table: "FirmaUsuario",
                column: "UsuarioId",
                unique: true,
                filter: "[Activa] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitud_FirmaUsuario_FirmaAprobadorId",
                table: "Solicitud",
                column: "FirmaAprobadorId",
                principalTable: "FirmaUsuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitud_FirmaUsuario_FirmaSolicitanteId",
                table: "Solicitud",
                column: "FirmaSolicitanteId",
                principalTable: "FirmaUsuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Solicitud_FirmaUsuario_FirmaAprobadorId",
                table: "Solicitud");

            migrationBuilder.DropForeignKey(
                name: "FK_Solicitud_FirmaUsuario_FirmaSolicitanteId",
                table: "Solicitud");

            migrationBuilder.DropTable(
                name: "FirmaUsuario");

            migrationBuilder.DropIndex(
                name: "IX_Solicitud_FirmaAprobadorId",
                table: "Solicitud");

            migrationBuilder.DropIndex(
                name: "IX_Solicitud_FirmaSolicitanteId",
                table: "Solicitud");

            migrationBuilder.DropColumn(
                name: "FirmaAprobadorId",
                table: "Solicitud");

            migrationBuilder.DropColumn(
                name: "FirmaSolicitanteId",
                table: "Solicitud");
        }
    }
}
