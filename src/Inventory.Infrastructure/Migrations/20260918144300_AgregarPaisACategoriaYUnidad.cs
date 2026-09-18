using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPaisACategoriaYUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Unidad_CodigoUnidad",
                table: "Unidad");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_CodigoCategoria",
                table: "Categoria");

            // defaultValue: 1 (Bolivia) — no 0, que no es un Pais real. Backfillea toda
            // fila ya existente (Categoria/Unidad creadas antes de este módulo) para que
            // el AddForeignKey de más abajo no falle contra un PaisId inexistente.
            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Unidad",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Categoria",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "Unidad",
                keyColumn: "Id",
                keyValue: 1,
                column: "PaisId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Unidad",
                keyColumn: "Id",
                keyValue: 2,
                column: "PaisId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Unidad",
                keyColumn: "Id",
                keyValue: 3,
                column: "PaisId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Unidad_PaisId_CodigoUnidad",
                table: "Unidad",
                columns: new[] { "PaisId", "CodigoUnidad" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_PaisId_CodigoCategoria",
                table: "Categoria",
                columns: new[] { "PaisId", "CodigoCategoria" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Categoria_Pais_PaisId",
                table: "Categoria",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Unidad_Pais_PaisId",
                table: "Unidad",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categoria_Pais_PaisId",
                table: "Categoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Unidad_Pais_PaisId",
                table: "Unidad");

            migrationBuilder.DropIndex(
                name: "IX_Unidad_PaisId_CodigoUnidad",
                table: "Unidad");

            migrationBuilder.DropIndex(
                name: "IX_Categoria_PaisId_CodigoCategoria",
                table: "Categoria");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Unidad");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Unidad_CodigoUnidad",
                table: "Unidad",
                column: "CodigoUnidad",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Categoria_CodigoCategoria",
                table: "Categoria",
                column: "CodigoCategoria",
                unique: true,
                filter: "[Activo] = 1");
        }
    }
}
