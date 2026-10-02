using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConteoConSesionYEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SesionConteo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CerradoPorId = table.Column<int>(type: "int", nullable: true),
                    CerradoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConteoOrigenId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionConteo", x => x.Id);
                    table.CheckConstraint("CK_SesionConteo_Estado", "[Estado] IN (1, 2, 3)");
                    table.CheckConstraint("CK_SesionConteo_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
                    table.CheckConstraint("CK_SesionConteo_Resolucion", "[Estado] = 1 OR [FechaCierre] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_SesionConteo_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionConteo_SesionConteo_ConteoOrigenId",
                        column: x => x.ConteoOrigenId,
                        principalTable: "SesionConteo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionConteo_Usuario_CerradoPorId",
                        column: x => x.CerradoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionConteo_Usuario_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionConteoEvidencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SesionConteoId = table.Column<int>(type: "int", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Datos = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SubidoPorId = table.Column<int>(type: "int", nullable: false),
                    SubidoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionConteoEvidencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionConteoEvidencia_SesionConteo_SesionConteoId",
                        column: x => x.SesionConteoId,
                        principalTable: "SesionConteo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SesionConteoEvidencia_Usuario_SubidoPorId",
                        column: x => x.SubidoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SesionConteoLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SesionConteoId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: false),
                    ExistenciaSistema = table.Column<int>(type: "int", nullable: false),
                    CantidadContada = table.Column<int>(type: "int", nullable: true),
                    ContadoPorId = table.Column<int>(type: "int", nullable: true),
                    ContadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaConteo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionConteoLinea", x => x.Id);
                    table.CheckConstraint("CK_SesionConteoLinea_Cantidad", "[CantidadContada] IS NULL OR [CantidadContada] >= 0");
                    table.ForeignKey(
                        name: "FK_SesionConteoLinea_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionConteoLinea_SesionConteo_SesionConteoId",
                        column: x => x.SesionConteoId,
                        principalTable: "SesionConteo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SesionConteoLinea_Ubicacion_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionConteoLinea_Usuario_ContadoPorId",
                        column: x => x.ContadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteo_CerradoPorId",
                table: "SesionConteo",
                column: "CerradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteo_Codigo",
                table: "SesionConteo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteo_ConteoOrigenId",
                table: "SesionConteo",
                column: "ConteoOrigenId",
                unique: true,
                filter: "[Estado] = 1 AND [ConteoOrigenId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteo_CreadoPorId",
                table: "SesionConteo",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteo_PaisId_FechaCreacion",
                table: "SesionConteo",
                columns: new[] { "PaisId", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoEvidencia_SesionConteoId",
                table: "SesionConteoEvidencia",
                column: "SesionConteoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoEvidencia_SubidoPorId",
                table: "SesionConteoEvidencia",
                column: "SubidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoLinea_ContadoPorId",
                table: "SesionConteoLinea",
                column: "ContadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoLinea_ProductoId",
                table: "SesionConteoLinea",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoLinea_SesionConteoId_ProductoId_UbicacionId",
                table: "SesionConteoLinea",
                columns: new[] { "SesionConteoId", "ProductoId", "UbicacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionConteoLinea_UbicacionId",
                table: "SesionConteoLinea",
                column: "UbicacionId");

            // Los conteos del módulo anterior (filas sueltas agrupadas por un texto de sesión)
            // se conservan como conteos CERRADOS del modelo nuevo — sin evidencia, porque
            // entonces no existía. La existencia de cada línea es la actual (así la calculaba
            // antes la pantalla). Recién después se elimina la tabla vieja.
            migrationBuilder.Sql(@"
;WITH primeros AS (
    SELECT c.SesionConteo, c.ContadoPorId, c.ContadoPorNombre, c.FechaConteo,
           ROW_NUMBER() OVER (PARTITION BY c.SesionConteo ORDER BY c.FechaConteo, c.Id) AS rn
    FROM Conteo c
), ultimos AS (
    SELECT c.SesionConteo, c.ContadoPorId, c.ContadoPorNombre, c.FechaConteo,
           ROW_NUMBER() OVER (PARTITION BY c.SesionConteo ORDER BY c.FechaConteo DESC, c.Id DESC) AS rn
    FROM Conteo c
)
INSERT INTO SesionConteo (Codigo, PaisId, Nombre, Notas, Estado, CreadoPorId, CreadoPorNombre, FechaCreacion,
                          CerradoPorId, CerradoPorNombre, FechaCierre)
SELECT pr.SesionConteo,
       (SELECT TOP 1 pd.PaisId FROM Conteo c2 JOIN Producto pd ON pd.Id = c2.ProductoId WHERE c2.SesionConteo = pr.SesionConteo),
       NULL, N'Migrado del módulo de conteo anterior (sin evidencia adjunta).', 2,
       pr.ContadoPorId, pr.ContadoPorNombre, pr.FechaConteo,
       ul.ContadoPorId, ul.ContadoPorNombre, ul.FechaConteo
FROM primeros pr
JOIN ultimos ul ON ul.SesionConteo = pr.SesionConteo AND ul.rn = 1
WHERE pr.rn = 1;");

            migrationBuilder.Sql(@"
INSERT INTO SesionConteoLinea (SesionConteoId, ProductoId, UbicacionId, ExistenciaSistema, CantidadContada,
                               ContadoPorId, ContadoPorNombre, FechaConteo)
SELECT s.Id, c.ProductoId, c.UbicacionId,
       ISNULL((SELECT SUM(m.CantidadEfectiva) FROM Movimiento m WHERE m.ProductoId = c.ProductoId AND m.UbicacionId = c.UbicacionId), 0),
       c.CantidadContada, c.ContadoPorId, c.ContadoPorNombre, c.FechaConteo
FROM Conteo c
JOIN SesionConteo s ON s.Codigo = c.SesionConteo
WHERE c.NumeroConteo = (SELECT MAX(c2.NumeroConteo) FROM Conteo c2
                        WHERE c2.SesionConteo = c.SesionConteo AND c2.ProductoId = c.ProductoId AND c2.UbicacionId = c.UbicacionId);");

            migrationBuilder.DropTable(
                name: "Conteo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionConteoEvidencia");

            migrationBuilder.DropTable(
                name: "SesionConteoLinea");

            migrationBuilder.DropTable(
                name: "SesionConteo");

            migrationBuilder.CreateTable(
                name: "Conteo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContadoPorId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    UbicacionId = table.Column<int>(type: "int", nullable: false),
                    CantidadContada = table.Column<int>(type: "int", nullable: false),
                    ContadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaConteo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumeroConteo = table.Column<int>(type: "int", nullable: false),
                    SesionConteo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conteo", x => x.Id);
                    table.CheckConstraint("CK_Conteo_Cantidad", "[CantidadContada] >= 0");
                    table.CheckConstraint("CK_Conteo_Numero", "[NumeroConteo] >= 1");
                    table.ForeignKey(
                        name: "FK_Conteo_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conteo_Ubicacion_UbicacionId",
                        column: x => x.UbicacionId,
                        principalTable: "Ubicacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conteo_Usuario_ContadoPorId",
                        column: x => x.ContadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_ContadoPorId",
                table: "Conteo",
                column: "ContadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_ProductoId",
                table: "Conteo",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_SesionConteo_ProductoId_UbicacionId_NumeroConteo",
                table: "Conteo",
                columns: new[] { "SesionConteo", "ProductoId", "UbicacionId", "NumeroConteo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conteo_UbicacionId",
                table: "Conteo",
                column: "UbicacionId");
        }
    }
}
