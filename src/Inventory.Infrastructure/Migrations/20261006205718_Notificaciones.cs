using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Notificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaisId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Severidad = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Valor = table.Column<int>(type: "int", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaLeida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Resuelta = table.Column<bool>(type: "bit", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.Id);
                    table.CheckConstraint("CK_Notificacion_Categoria", "[Categoria] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Notificacion_Resolucion", "[Resuelta] = 0 OR [FechaResolucion] IS NOT NULL");
                    table.CheckConstraint("CK_Notificacion_Severidad", "[Severidad] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Notificacion_Pais_PaisId",
                        column: x => x.PaisId,
                        principalTable: "Pais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notificacion_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_PaisId_Resuelta",
                table: "Notificacion",
                columns: new[] { "PaisId", "Resuelta" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_UsuarioId_Clave",
                table: "Notificacion",
                columns: new[] { "UsuarioId", "Clave" },
                unique: true,
                filter: "[Resuelta] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_UsuarioId_Resuelta_FechaCreacion",
                table: "Notificacion",
                columns: new[] { "UsuarioId", "Resuelta", "FechaCreacion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notificacion");
        }
    }
}
