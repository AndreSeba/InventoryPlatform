using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVencimientoAMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaVencimiento",
                table: "Movimiento",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_VencimientoSoloEntrada",
                table: "Movimiento",
                sql: "[TipoMovimiento] = 1 OR [FechaVencimiento] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Movimiento_VencimientoSoloEntrada",
                table: "Movimiento");

            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                table: "Movimiento");
        }
    }
}
