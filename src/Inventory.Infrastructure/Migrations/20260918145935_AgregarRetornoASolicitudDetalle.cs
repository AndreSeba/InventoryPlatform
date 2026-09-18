using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRetornoASolicitudDetalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaRetornoEsperada",
                table: "SolicitudDetalle",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Retorna",
                table: "SolicitudDetalle",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionExterna",
                table: "SolicitudDetalle",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SolicitudDetalle_RetornaSoloConDatos",
                table: "SolicitudDetalle",
                sql: "[Retorna] = 1 OR ([UbicacionExterna] IS NULL AND [FechaRetornoEsperada] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SolicitudDetalle_RetornaSoloConDatos",
                table: "SolicitudDetalle");

            migrationBuilder.DropColumn(
                name: "FechaRetornoEsperada",
                table: "SolicitudDetalle");

            migrationBuilder.DropColumn(
                name: "Retorna",
                table: "SolicitudDetalle");

            migrationBuilder.DropColumn(
                name: "UbicacionExterna",
                table: "SolicitudDetalle");
        }
    }
}
