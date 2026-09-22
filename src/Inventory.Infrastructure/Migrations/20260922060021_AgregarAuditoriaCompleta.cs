using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAuditoriaCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Auditoria",
                type: "int",
                nullable: false,
                defaultValue: 1); // backfill a Bolivia — las únicas filas existentes hoy son de ProductoService

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[] { 37, "auditoria.ver", "Ver el historial de auditoría de todos los módulos", "Auditoría" });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 37, 1 },
                    { 37, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_PaisId_FechaHora",
                table: "Auditoria",
                columns: new[] { "PaisId", "FechaHora" });

            migrationBuilder.AddForeignKey(
                name: "FK_Auditoria_Pais_PaisId",
                table: "Auditoria",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auditoria_Pais_PaisId",
                table: "Auditoria");

            migrationBuilder.DropIndex(
                name: "IX_Auditoria_PaisId_FechaHora",
                table: "Auditoria");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 37, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 37, 5 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Auditoria");
        }
    }
}
