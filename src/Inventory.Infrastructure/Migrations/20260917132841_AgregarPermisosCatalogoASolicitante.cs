using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPermisosCatalogoASolicitante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 17, 4 },
                    { 28, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 17, 4 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 4 });
        }
    }
}
