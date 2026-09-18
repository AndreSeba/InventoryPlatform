using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPaisYAlmacen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ubicacion_CodigoUbicacion",
                table: "Ubicacion");

            migrationBuilder.DropIndex(
                name: "IX_Producto_ClaveProducto",
                table: "Producto");

            migrationBuilder.DropIndex(
                name: "IX_Area_CodigoArea",
                table: "Area");

            // defaultValue: 1 (no 0) a propósito: backfillea cualquier Ubicacion/Producto/
            // Area ya existente al almacén propio / país de Bolivia (Id=1, sembrados más
            // abajo en esta misma migración) — hasta hoy el sistema solo operó ahí. Un
            // defaultValue: 0 dejaría filas apuntando a un Almacen/Pais inexistente y el
            // AddForeignKey del final de este método fallaría contra cualquier base con
            // datos reales cargados.
            migrationBuilder.AddColumn<int>(
                name: "AlmacenId",
                table: "Ubicacion",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Producto",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PaisId",
                table: "Area",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Pais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CodigoIso = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Almacen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoAlmacen = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    TipoAlmacen = table.Column<int>(type: "int", nullable: false),
                    ProveedorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ProveedorContacto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ProveedorDireccion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Almacen", x => x.Id);
                    table.CheckConstraint("CK_Almacen_Proveedor", "([TipoAlmacen] = 1 AND [ProveedorNombre] IS NULL) OR ([TipoAlmacen] = 2 AND [ProveedorNombre] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Almacen_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Pais",
                columns: new[] { "Id", "Activo", "CodigoIso", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "BO", "Bolivia" },
                    { 2, true, "PE", "Perú" }
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[,]
                {
                    { 31, "almacenes.ver", "Ver almacenes", "Almacenes" },
                    { 32, "almacenes.crear", "Crear almacenes", "Almacenes" },
                    { 33, "almacenes.editar", "Editar almacenes existentes", "Almacenes" },
                    { 34, "paises.ver", "Ver países", "Países" },
                    { 35, "paises.crear", "Crear países", "Países" },
                    { 36, "paises.editar", "Editar países existentes", "Países" }
                });

            migrationBuilder.InsertData(
                table: "Almacen",
                columns: new[] { "Id", "Activo", "CodigoAlmacen", "Nombre", "PaisId", "ProveedorContacto", "ProveedorDireccion", "ProveedorNombre", "TipoAlmacen" },
                values: new object[] { 1, true, "BO-PROPIO", "Almacén Nestlé Bolivia", 1, null, null, null, 1 });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 31, 1 },
                    { 32, 1 },
                    { 33, 1 },
                    { 34, 1 },
                    { 35, 1 },
                    { 36, 1 },
                    { 31, 2 },
                    { 31, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ubicacion_AlmacenId_CodigoUbicacion",
                table: "Ubicacion",
                columns: new[] { "AlmacenId", "CodigoUbicacion" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_PaisId_ClaveProducto",
                table: "Producto",
                columns: new[] { "PaisId", "ClaveProducto" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Area_PaisId_CodigoArea",
                table: "Area",
                columns: new[] { "PaisId", "CodigoArea" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Almacen_CodigoAlmacen",
                table: "Almacen",
                column: "CodigoAlmacen",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Almacen_PaisId",
                table: "Almacen",
                column: "PaisId");

            migrationBuilder.CreateIndex(
                name: "IX_Pais_CodigoIso",
                table: "Pais",
                column: "CodigoIso",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Area_Pais_PaisId",
                table: "Area",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Producto_Pais_PaisId",
                table: "Producto",
                column: "PaisId",
                principalTable: "Pais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ubicacion_Almacen_AlmacenId",
                table: "Ubicacion",
                column: "AlmacenId",
                principalTable: "Almacen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Area_Pais_PaisId",
                table: "Area");

            migrationBuilder.DropForeignKey(
                name: "FK_Producto_Pais_PaisId",
                table: "Producto");

            migrationBuilder.DropForeignKey(
                name: "FK_Ubicacion_Almacen_AlmacenId",
                table: "Ubicacion");

            migrationBuilder.DropTable(
                name: "Almacen");

            migrationBuilder.DropTable(
                name: "Pais");

            migrationBuilder.DropIndex(
                name: "IX_Ubicacion_AlmacenId_CodigoUbicacion",
                table: "Ubicacion");

            migrationBuilder.DropIndex(
                name: "IX_Producto_PaisId_ClaveProducto",
                table: "Producto");

            migrationBuilder.DropIndex(
                name: "IX_Area_PaisId_CodigoArea",
                table: "Area");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 32, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 33, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 34, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 35, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 36, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 2 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 31, 3 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DropColumn(
                name: "AlmacenId",
                table: "Ubicacion");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Producto");

            migrationBuilder.DropColumn(
                name: "PaisId",
                table: "Area");

            migrationBuilder.CreateIndex(
                name: "IX_Ubicacion_CodigoUbicacion",
                table: "Ubicacion",
                column: "CodigoUbicacion",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_ClaveProducto",
                table: "Producto",
                column: "ClaveProducto",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Area_CodigoArea",
                table: "Area",
                column: "CodigoArea",
                unique: true,
                filter: "[Activo] = 1");
        }
    }
}
