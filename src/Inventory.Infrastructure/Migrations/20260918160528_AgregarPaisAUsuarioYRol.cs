using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPaisAUsuarioYRol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuario_Email",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "IX_Rol_Nombre",
                table: "Rol");

            // defaultValue: 1 (Bolivia) — backfillea todo Usuario/Rol ya existente, para
            // que el AddForeignKey de más abajo no falle contra un PaisId inexistente.
            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Usuario",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Rol",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 1,
                column: "PaisId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 2,
                column: "PaisId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 3,
                column: "PaisId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 4,
                column: "PaisId",
                value: 1);

            migrationBuilder.InsertData(
                table: "Rol",
                columns: new[] { "Id", "Activo", "Descripcion", "Nombre", "PaisId" },
                values: new object[,]
                {
                    { 5, true, "Acceso completo, incluida la gestión de usuarios y roles.", "Administrador", 2 },
                    { 6, true, "Opera el día a día: productos, movimientos, solicitudes y conteo. Sin gestión de catálogos ni usuarios.", "Operador", 2 },
                    { 7, true, "Solo lectura en todos los módulos.", "Consulta", 2 },
                    { 8, true, "Solo puede crear y ver sus propias solicitudes de material.", "Solicitante", 2 }
                });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 1, 5 },
                    { 2, 5 },
                    { 3, 5 },
                    { 4, 5 },
                    { 5, 5 },
                    { 6, 5 },
                    { 7, 5 },
                    { 8, 5 },
                    { 9, 5 },
                    { 10, 5 },
                    { 11, 5 },
                    { 12, 5 },
                    { 13, 5 },
                    { 14, 5 },
                    { 15, 5 },
                    { 16, 5 },
                    { 17, 5 },
                    { 18, 5 },
                    { 19, 5 },
                    { 20, 5 },
                    { 21, 5 },
                    { 22, 5 },
                    { 23, 5 },
                    { 24, 5 },
                    { 25, 5 },
                    { 26, 5 },
                    { 27, 5 },
                    { 28, 5 },
                    { 29, 5 },
                    { 30, 5 },
                    { 31, 5 },
                    { 32, 5 },
                    { 33, 5 },
                    { 34, 5 },
                    { 35, 5 },
                    { 36, 5 },
                    { 1, 6 },
                    { 5, 6 },
                    { 6, 6 },
                    { 7, 6 },
                    { 8, 6 },
                    { 9, 6 },
                    { 10, 6 },
                    { 11, 6 },
                    { 14, 6 },
                    { 15, 6 },
                    { 16, 6 },
                    { 17, 6 },
                    { 19, 6 },
                    { 21, 6 },
                    { 27, 6 },
                    { 28, 6 },
                    { 31, 6 },
                    { 1, 7 },
                    { 5, 7 },
                    { 10, 7 },
                    { 15, 7 },
                    { 17, 7 },
                    { 19, 7 },
                    { 21, 7 },
                    { 27, 7 },
                    { 28, 7 },
                    { 31, 7 },
                    { 1, 8 },
                    { 11, 8 },
                    { 17, 8 },
                    { 19, 8 },
                    { 21, 8 },
                    { 28, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_PaisId_Email",
                table: "Usuario",
                columns: new[] { "PaisId", "Email" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Rol_PaisId_Nombre",
                table: "Rol",
                columns: new[] { "PaisId", "Nombre" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Rol_Pais_PaisId",
                table: "Rol",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuario_Pais_PaisId",
                table: "Usuario",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rol_Pais_PaisId",
                table: "Rol");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuario_Pais_PaisId",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "IX_Usuario_PaisId_Email",
                table: "Usuario");

            migrationBuilder.DropIndex(
                name: "IX_Rol_PaisId_Nombre",
                table: "Rol");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 1, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 2, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 3, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 4, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 5, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 6, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 7, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 8, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 9, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 10, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 11, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 12, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 13, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 14, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 15, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 16, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 17, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 18, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 19, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 20, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 21, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 22, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 23, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 24, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 25, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 26, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 27, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 29, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 30, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 32, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 33, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 34, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 35, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 36, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 1, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 5, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 6, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 7, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 8, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 9, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 10, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 11, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 14, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 15, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 16, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 17, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 19, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 21, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 27, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 1, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 5, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 10, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 15, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 17, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 19, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 21, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 27, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 7 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 1, 8 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 11, 8 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 17, 8 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 19, 8 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 21, 8 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 8 });

            migrationBuilder.DeleteData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Rol",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Rol");

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_Email",
                table: "Usuario",
                column: "Email",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Rol_Nombre",
                table: "Rol",
                column: "Nombre",
                unique: true,
                filter: "[Activo] = 1");
        }
    }
}
