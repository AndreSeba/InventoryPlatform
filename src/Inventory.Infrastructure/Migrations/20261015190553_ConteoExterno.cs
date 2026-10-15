using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConteoExterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConteoExternoAlmacen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoAlmacen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteoExternoAlmacen_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Familia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    UnidadCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CostoReferencia = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoProducto", x => x.Id);
                    table.CheckConstraint("CK_ConteoExternoProducto_CostoNoNegativo", "[CostoReferencia] IS NULL OR [CostoReferencia] >= 0");
                    table.ForeignKey(
                        name: "FK_ConteoExternoProducto_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoSesion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CerradoPorId = table.Column<int>(type: "int", nullable: true),
                    CerradoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoSesion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteoExternoSesion_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConteoExternoSesion_Usuario_CerradoPorId",
                        column: x => x.CerradoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConteoExternoSesion_Usuario_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoUnidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DecimalesPermitidos = table.Column<int>(type: "int", nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoUnidad", x => x.Id);
                    table.CheckConstraint("CK_ConteoExternoUnidad_Decimales", "[DecimalesPermitidos] >= 0 AND [DecimalesPermitidos] <= 4");
                    table.ForeignKey(
                        name: "FK_ConteoExternoUnidad_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoExistencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConteoExternoProductoId = table.Column<int>(type: "int", nullable: false),
                    AlmacenCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Lote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CantidadSistema = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoExistencia", x => x.Id);
                    table.CheckConstraint("CK_ConteoExternoExistencia_Cantidad", "[CantidadSistema] >= 0");
                    table.ForeignKey(
                        name: "FK_ConteoExternoExistencia_ConteoExternoProducto_ConteoExternoProductoId",
                        column: x => x.ConteoExternoProductoId,
                        principalTable: "ConteoExternoProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConteoExternoExistencia_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConteoExternoSesionId = table.Column<int>(type: "int", nullable: false),
                    ConteoExternoExistenciaId = table.Column<int>(type: "int", nullable: false),
                    ConteoExternoProductoId = table.Column<int>(type: "int", nullable: false),
                    ProductoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Familia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AlmacenCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Lote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnidadCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExistenciaSistema = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteoExternoLinea_ConteoExternoExistencia_ConteoExternoExistenciaId",
                        column: x => x.ConteoExternoExistenciaId,
                        principalTable: "ConteoExternoExistencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConteoExternoLinea_ConteoExternoSesion_ConteoExternoSesionId",
                        column: x => x.ConteoExternoSesionId,
                        principalTable: "ConteoExternoSesion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConteoExternoRegistro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConteoExternoLineaId = table.Column<int>(type: "int", nullable: false),
                    NumeroConteo = table.Column<int>(type: "int", nullable: false),
                    Ubicacion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ContadoPorId = table.Column<int>(type: "int", nullable: false),
                    ContadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaConteo = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteoExternoRegistro", x => x.Id);
                    table.CheckConstraint("CK_ConteoExternoRegistro_Numero", "[NumeroConteo] >= 1 AND [NumeroConteo] <= 4");
                    table.ForeignKey(
                        name: "FK_ConteoExternoRegistro_ConteoExternoLinea_ConteoExternoLineaId",
                        column: x => x.ConteoExternoLineaId,
                        principalTable: "ConteoExternoLinea",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConteoExternoRegistro_Usuario_ContadoPorId",
                        column: x => x.ContadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[,]
                {
                    { 40, "conteoexterno.ver", "Ver el catálogo propio, existencias y sesiones del módulo de conteo externo", "Conteo Externo" },
                    { 41, "conteoexterno.gestionar", "Crear y editar catálogo, existencias y sesiones del módulo de conteo externo", "Conteo Externo" }
                });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 40, 1 },
                    { 41, 1 },
                    { 40, 5 },
                    { 41, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoAlmacen_PaisId_Codigo",
                table: "ConteoExternoAlmacen",
                columns: new[] { "PaisId", "Codigo" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoExistencia_ConteoExternoProductoId",
                table: "ConteoExternoExistencia",
                column: "ConteoExternoProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoExistencia_PaisId_ConteoExternoProductoId_AlmacenCodigo",
                table: "ConteoExternoExistencia",
                columns: new[] { "PaisId", "ConteoExternoProductoId", "AlmacenCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoLinea_ConteoExternoExistenciaId",
                table: "ConteoExternoLinea",
                column: "ConteoExternoExistenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoLinea_ConteoExternoSesionId_ConteoExternoExistenciaId",
                table: "ConteoExternoLinea",
                columns: new[] { "ConteoExternoSesionId", "ConteoExternoExistenciaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoProducto_PaisId_CodigoNormalizado",
                table: "ConteoExternoProducto",
                columns: new[] { "PaisId", "CodigoNormalizado" },
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoProducto_PaisId_Familia",
                table: "ConteoExternoProducto",
                columns: new[] { "PaisId", "Familia" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoRegistro_ContadoPorId",
                table: "ConteoExternoRegistro",
                column: "ContadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoRegistro_ConteoExternoLineaId_NumeroConteo_Ubicacion",
                table: "ConteoExternoRegistro",
                columns: new[] { "ConteoExternoLineaId", "NumeroConteo", "Ubicacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoSesion_CerradoPorId",
                table: "ConteoExternoSesion",
                column: "CerradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoSesion_CreadoPorId",
                table: "ConteoExternoSesion",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoSesion_PaisId",
                table: "ConteoExternoSesion",
                column: "PaisId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteoExternoUnidad_PaisId_Codigo",
                table: "ConteoExternoUnidad",
                columns: new[] { "PaisId", "Codigo" },
                unique: true,
                filter: "[Activo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConteoExternoAlmacen");

            migrationBuilder.DropTable(
                name: "ConteoExternoRegistro");

            migrationBuilder.DropTable(
                name: "ConteoExternoUnidad");

            migrationBuilder.DropTable(
                name: "ConteoExternoLinea");

            migrationBuilder.DropTable(
                name: "ConteoExternoExistencia");

            migrationBuilder.DropTable(
                name: "ConteoExternoSesion");

            migrationBuilder.DropTable(
                name: "ConteoExternoProducto");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 40, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 41, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 40, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 41, 5 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 41);
        }
    }
}
