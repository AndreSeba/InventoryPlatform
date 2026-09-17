using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TraerCambiosRamaContinuacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Producto_Unidad",
                table: "Producto");

            // Movimiento.RegistradoPor, Solicitud.SolicitadoPor/AprobadoPor y
            // Conteo.ContadoPor guardaban el NOMBRE como texto libre — el rename conserva
            // ese valor tal cual, se convierte en el snapshot histórico ("con qué nombre
            // se firmó en su momento"). Las columnas ...Id nuevas son el FK real, todavía
            // sin backfillear en este punto.
            migrationBuilder.RenameColumn(
                name: "SolicitadoPor",
                table: "Solicitud",
                newName: "SolicitadoPorNombre");

            migrationBuilder.RenameColumn(
                name: "AprobadoPor",
                table: "Solicitud",
                newName: "AprobadoPorNombre");

            migrationBuilder.RenameColumn(
                name: "RegistradoPor",
                table: "Movimiento",
                newName: "RegistradoPorNombre");

            migrationBuilder.RenameColumn(
                name: "ContadoPor",
                table: "Conteo",
                newName: "ContadoPorNombre");

            migrationBuilder.AddColumn<int>(
                name: "AprobadoPorId",
                table: "Solicitud",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SolicitadoPorId",
                table: "Solicitud",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RegistradoPorId",
                table: "Movimiento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContadoPorId",
                table: "Conteo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Auditoria.UsuarioId guardaba el NOMBRE en una columna string (pese al nombre)
            // — a diferencia de las otras 3 tablas no se puede "renombrar" porque el tipo
            // cambia de nvarchar a int y SQL Server no convierte "Administrador" a un int
            // implícito. Se arma con una columna nueva temporal + backfill + swap, en vez
            // del AlterColumn directo que generó el scaffolding (que hubiera roto contra
            // cualquier fila existente).
            migrationBuilder.AddColumn<string>(
                name: "UsuarioNombre",
                table: "Auditoria",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql("UPDATE [Auditoria] SET [UsuarioNombre] = [UsuarioId];");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioIdNuevo",
                table: "Auditoria",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Unidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoUnidad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidad", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[,]
                {
                    { 28, "unidades.ver", "Ver las unidades de medida", "Unidades" },
                    { 29, "unidades.crear", "Crear unidades de medida", "Unidades" },
                    { 30, "unidades.editar", "Editar unidades de medida existentes", "Unidades" }
                });

            migrationBuilder.InsertData(
                table: "Unidad",
                columns: new[] { "Id", "Activo", "CodigoUnidad", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "UNI", "Unidad" },
                    { 2, true, "CAJA", "Caja" },
                    { 3, true, "PQTS", "Paquete" }
                });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 28, 1 },
                    { 29, 1 },
                    { 30, 1 },
                    { 28, 2 },
                    { 28, 3 }
                });

            // Backfill por nombre (2026-09-16/17, verificado antes de escribir esta
            // migración contra la base local: los valores históricos de
            // Movimiento/Solicitud/Conteo/Auditoria matchean exacto con un Usuario real —
            // cero pérdida de datos, no hizo falta vaciar nada).
            migrationBuilder.Sql(@"
                UPDATE s SET s.[SolicitadoPorId] = u.[Id]
                FROM [Solicitud] s JOIN [Usuario] u ON u.[NombreCompleto] = s.[SolicitadoPorNombre];

                UPDATE s SET s.[AprobadoPorId] = u.[Id]
                FROM [Solicitud] s JOIN [Usuario] u ON u.[NombreCompleto] = s.[AprobadoPorNombre]
                WHERE s.[AprobadoPorNombre] IS NOT NULL;

                UPDATE m SET m.[RegistradoPorId] = u.[Id]
                FROM [Movimiento] m JOIN [Usuario] u ON u.[NombreCompleto] = m.[RegistradoPorNombre];

                UPDATE c SET c.[ContadoPorId] = u.[Id]
                FROM [Conteo] c JOIN [Usuario] u ON u.[NombreCompleto] = c.[ContadoPorNombre];

                UPDATE a SET a.[UsuarioIdNuevo] = u.[Id]
                FROM [Auditoria] a JOIN [Usuario] u ON u.[NombreCompleto] = a.[UsuarioId];
            ");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Auditoria");

            migrationBuilder.RenameColumn(
                name: "UsuarioIdNuevo",
                table: "Auditoria",
                newName: "UsuarioId");

            migrationBuilder.AlterColumn<int>(
                name: "UsuarioId",
                table: "Auditoria",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UsuarioNombre",
                table: "Auditoria",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_AprobadoPorId",
                table: "Solicitud",
                column: "AprobadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitud_SolicitadoPorId",
                table: "Solicitud",
                column: "SolicitadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Movimiento_RegistradoPorId",
                table: "Movimiento",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_ContadoPorId",
                table: "Conteo",
                column: "ContadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_UsuarioId",
                table: "Auditoria",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Unidad_CodigoUnidad",
                table: "Unidad",
                column: "CodigoUnidad",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Auditoria_Usuario_UsuarioId",
                table: "Auditoria",
                column: "UsuarioId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conteo_Usuario_ContadoPorId",
                table: "Conteo",
                column: "ContadoPorId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movimiento_Usuario_RegistradoPorId",
                table: "Movimiento",
                column: "RegistradoPorId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitud_Usuario_AprobadoPorId",
                table: "Solicitud",
                column: "AprobadoPorId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Solicitud_Usuario_SolicitadoPorId",
                table: "Solicitud",
                column: "SolicitadoPorId",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auditoria_Usuario_UsuarioId",
                table: "Auditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Conteo_Usuario_ContadoPorId",
                table: "Conteo");

            migrationBuilder.DropForeignKey(
                name: "FK_Movimiento_Usuario_RegistradoPorId",
                table: "Movimiento");

            migrationBuilder.DropForeignKey(
                name: "FK_Solicitud_Usuario_AprobadoPorId",
                table: "Solicitud");

            migrationBuilder.DropForeignKey(
                name: "FK_Solicitud_Usuario_SolicitadoPorId",
                table: "Solicitud");

            migrationBuilder.DropTable(
                name: "Unidad");

            migrationBuilder.DropIndex(
                name: "IX_Solicitud_AprobadoPorId",
                table: "Solicitud");

            migrationBuilder.DropIndex(
                name: "IX_Solicitud_SolicitadoPorId",
                table: "Solicitud");

            migrationBuilder.DropIndex(
                name: "IX_Movimiento_RegistradoPorId",
                table: "Movimiento");

            migrationBuilder.DropIndex(
                name: "IX_Conteo_ContadoPorId",
                table: "Conteo");

            migrationBuilder.DropIndex(
                name: "IX_Auditoria_UsuarioId",
                table: "Auditoria");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 29, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 30, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 2 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 28, 3 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DropColumn(
                name: "AprobadoPorId",
                table: "Solicitud");

            migrationBuilder.DropColumn(
                name: "SolicitadoPorId",
                table: "Solicitud");

            migrationBuilder.DropColumn(
                name: "RegistradoPorId",
                table: "Movimiento");

            migrationBuilder.DropColumn(
                name: "ContadoPorId",
                table: "Conteo");

            // Reconstruye el UsuarioId string viejo a partir del FK + Usuario.NombreCompleto
            // ACTUAL (no necesariamente igual al snapshot si el usuario cambió de nombre
            // después de migrar — límite aceptado de un rollback, no del camino normal).
            migrationBuilder.AddColumn<string>(
                name: "UsuarioIdViejo",
                table: "Auditoria",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE a SET a.[UsuarioIdViejo] = u.[NombreCompleto]
                FROM [Auditoria] a JOIN [Usuario] u ON u.[Id] = a.[UsuarioId];
            ");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Auditoria");

            migrationBuilder.DropColumn(
                name: "UsuarioNombre",
                table: "Auditoria");

            migrationBuilder.RenameColumn(
                name: "UsuarioIdViejo",
                table: "Auditoria",
                newName: "UsuarioId");

            migrationBuilder.AlterColumn<string>(
                name: "UsuarioId",
                table: "Auditoria",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "SolicitadoPorNombre",
                table: "Solicitud",
                newName: "SolicitadoPor");

            migrationBuilder.RenameColumn(
                name: "AprobadoPorNombre",
                table: "Solicitud",
                newName: "AprobadoPor");

            migrationBuilder.RenameColumn(
                name: "RegistradoPorNombre",
                table: "Movimiento",
                newName: "RegistradoPor");

            migrationBuilder.RenameColumn(
                name: "ContadoPorNombre",
                table: "Conteo",
                newName: "ContadoPor");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Producto_Unidad",
                table: "Producto",
                sql: "[UnidadMedida] IN ('UNI','CAJA','PQTS')");
        }
    }
}
