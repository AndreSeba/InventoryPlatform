using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AvisoDeDevolucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisoDevolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    MovimientoOrigenId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    AvisadoPorId = table.Column<int>(type: "int", nullable: false),
                    AvisadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaAviso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResueltoPorId = table.Column<int>(type: "int", nullable: true),
                    ResueltoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CantidadRecibida = table.Column<int>(type: "int", nullable: true),
                    MovimientoDevolucionId = table.Column<int>(type: "int", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoDevolucion", x => x.Id);
                    table.CheckConstraint("CK_AvisoDevolucion_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_AvisoDevolucion_Estado", "[Estado] IN (1, 2, 3)");
                    table.CheckConstraint("CK_AvisoDevolucion_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
                    table.CheckConstraint("CK_AvisoDevolucion_Recibido", "[Estado] <> 2 OR ([MovimientoDevolucionId] IS NOT NULL AND [CantidadRecibida] > 0)");
                    table.CheckConstraint("CK_AvisoDevolucion_Resolucion", "[Estado] = 1 OR [FechaResolucion] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_AvisoDevolucion_Movimiento_MovimientoDevolucionId",
                        column: x => x.MovimientoDevolucionId,
                        principalTable: "Movimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoDevolucion_Movimiento_MovimientoOrigenId",
                        column: x => x.MovimientoOrigenId,
                        principalTable: "Movimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoDevolucion_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoDevolucion_Usuario_AvisadoPorId",
                        column: x => x.AvisadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoDevolucion_Usuario_ResueltoPorId",
                        column: x => x.ResueltoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[] { 39, "devoluciones.avisar", "Ver el material prestado a nombre propio y avisar su devolución", "Movimientos" });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 39, 1 },
                    { 39, 2 },
                    { 39, 4 },
                    { 39, 5 },
                    { 39, 6 },
                    { 39, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_AvisadoPorId",
                table: "AvisoDevolucion",
                column: "AvisadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_Codigo",
                table: "AvisoDevolucion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_MovimientoDevolucionId",
                table: "AvisoDevolucion",
                column: "MovimientoDevolucionId",
                unique: true,
                filter: "[MovimientoDevolucionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_MovimientoOrigenId",
                table: "AvisoDevolucion",
                column: "MovimientoOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_PaisId_Estado",
                table: "AvisoDevolucion",
                columns: new[] { "PaisId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_AvisoDevolucion_ResueltoPorId",
                table: "AvisoDevolucion",
                column: "ResueltoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvisoDevolucion");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 2 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 4 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 5 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 6 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 39, 8 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 39);
        }
    }
}
