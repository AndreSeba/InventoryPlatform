using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoSolicitudYEncargadoCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Movimiento_SolicitudSoloSalida",
                table: "Movimiento");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Solicitud",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "EncargadoId",
                table: "Categoria",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_SolicitudSoloSalida",
                table: "Movimiento",
                sql: "[SolicitudDetalleId] IS NULL OR [TipoMovimiento] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_EncargadoId",
                table: "Categoria",
                column: "EncargadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Usuario_EncargadoId",
                table: "Categoria",
                column: "EncargadoId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Usuario_EncargadoId",
                table: "Categoria");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Movimiento_SolicitudSoloSalida",
                table: "Movimiento");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_EncargadoId",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Solicitud");

            migrationBuilder.DropColumn(
                name: "EncargadoId",
                table: "Categoria");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_SolicitudSoloSalida",
                table: "Movimiento",
                sql: "[SolicitudDetalleId] IS NULL OR [TipoMovimiento] = 2");
        }
    }
}
