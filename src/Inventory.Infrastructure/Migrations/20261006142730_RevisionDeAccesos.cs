using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevisionDeAccesos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RevisionAcceso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    Alcance = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IniciadaPorId = table.Column<int>(type: "int", nullable: false),
                    IniciadaPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CerradaPorId = table.Column<int>(type: "int", nullable: true),
                    CerradaPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoCancelacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionAcceso", x => x.Id);
                    table.CheckConstraint("CK_RevisionAcceso_Alcance", "[Alcance] IN (1, 2)");
                    table.CheckConstraint("CK_RevisionAcceso_Estado", "[Estado] IN (1, 2, 3)");
                    table.CheckConstraint("CK_RevisionAcceso_MotivoCancelacion", "[Estado] <> 3 OR [MotivoCancelacion] IS NOT NULL");
                    table.CheckConstraint("CK_RevisionAcceso_Resolucion", "[Estado] = 1 OR [FechaCierre] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_RevisionAcceso_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionAcceso_Usuario_CerradaPorId",
                        column: x => x.CerradaPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionAcceso_Usuario_IniciadaPorId",
                        column: x => x.IniciadaPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevisionAccesoLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RevisionAccesoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    RolNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EsPrivilegiado = table.Column<bool>(type: "bit", nullable: false),
                    CuentaCreadaEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoLoginEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    RolNuevoId = table.Column<int>(type: "int", nullable: true),
                    RolNuevoNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevisadoPorId = table.Column<int>(type: "int", nullable: true),
                    RevisadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aplicada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionAccesoLinea", x => x.Id);
                    table.CheckConstraint("CK_RevisionAccesoLinea_Decision", "[Decision] IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_RevisionAccesoLinea_RolNuevo", "([Decision] = 3 AND [RolNuevoId] IS NOT NULL) OR ([Decision] <> 3 AND [RolNuevoId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_RevisionAccesoLinea_RevisionAcceso_RevisionAccesoId",
                        column: x => x.RevisionAccesoId,
                        principalTable: "RevisionAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RevisionAccesoLinea_Rol_RolNuevoId",
                        column: x => x.RolNuevoId,
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionAccesoLinea_Usuario_RevisadoPorId",
                        column: x => x.RevisadoPorId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionAccesoLinea_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permiso",
                columns: new[] { "Id", "Codigo", "Descripcion", "Modulo" },
                values: new object[] { 38, "accesos.revisar", "Abrir y resolver revisiones periódicas de accesos de usuarios", "Administración" });

            migrationBuilder.InsertData(
                table: "RolPermiso",
                columns: new[] { "PermisoId", "RolId" },
                values: new object[,]
                {
                    { 38, 1 },
                    { 38, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAcceso_CerradaPorId",
                table: "RevisionAcceso",
                column: "CerradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAcceso_Codigo",
                table: "RevisionAcceso",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAcceso_IniciadaPorId",
                table: "RevisionAcceso",
                column: "IniciadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAcceso_PaisId_Alcance",
                table: "RevisionAcceso",
                columns: new[] { "PaisId", "Alcance" },
                unique: true,
                filter: "[Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAcceso_PaisId_FechaInicio",
                table: "RevisionAcceso",
                columns: new[] { "PaisId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAccesoLinea_RevisadoPorId",
                table: "RevisionAccesoLinea",
                column: "RevisadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAccesoLinea_RevisionAccesoId_UsuarioId",
                table: "RevisionAccesoLinea",
                columns: new[] { "RevisionAccesoId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAccesoLinea_RolNuevoId",
                table: "RevisionAccesoLinea",
                column: "RolNuevoId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionAccesoLinea_UsuarioId",
                table: "RevisionAccesoLinea",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevisionAccesoLinea");

            migrationBuilder.DropTable(
                name: "RevisionAcceso");

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 38, 1 });

            migrationBuilder.DeleteData(
                table: "RolPermiso",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 38, 5 });

            migrationBuilder.DeleteData(
                table: "Permiso",
                keyColumn: "Id",
                keyValue: 38);
        }
    }
}
