using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CantidadesComoEntero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server no permite ALTER COLUMN sobre una columna que tiene un CHECK
            // constraint encima (aunque el CHECK no cambie de texto) — hay que sacarlos
            // antes de cada ALTER y volver a ponerlos con el mismo texto después.
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Solicitada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Aprobada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Entregada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_Producto_StockMin", table: "Producto");
            migrationBuilder.DropCheckConstraint(name: "CK_Movimiento_Cantidad", table: "Movimiento");
            migrationBuilder.DropCheckConstraint(name: "CK_Movimiento_Signo", table: "Movimiento");
            migrationBuilder.DropCheckConstraint(name: "CK_Conteo_Cantidad", table: "Conteo");

            migrationBuilder.AlterColumn<int>(
                name: "CantidadSolicitada",
                table: "SolicitudDetalle",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "CantidadEntregada",
                table: "SolicitudDetalle",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "CantidadAprobada",
                table: "SolicitudDetalle",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "StockMinimo",
                table: "Producto",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "CantidadEfectiva",
                table: "Movimiento",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "Cantidad",
                table: "Movimiento",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AlterColumn<int>(
                name: "CantidadContada",
                table: "Conteo",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)");

            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Solicitada", table: "SolicitudDetalle", sql: "[CantidadSolicitada] > 0");
            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Aprobada", table: "SolicitudDetalle", sql: "[CantidadAprobada] IS NULL OR [CantidadAprobada] <= [CantidadSolicitada]");
            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Entregada", table: "SolicitudDetalle", sql: "[CantidadEntregada] >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Producto_StockMin", table: "Producto", sql: "[StockMinimo] >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Movimiento_Cantidad", table: "Movimiento", sql: "[Cantidad] > 0");
            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_Signo",
                table: "Movimiento",
                sql: "([TipoMovimiento] IN (1,3) AND [CantidadEfectiva] = [Cantidad]) OR ([TipoMovimiento] IN (2,4) AND [CantidadEfectiva] = -[Cantidad])");
            migrationBuilder.AddCheckConstraint(name: "CK_Conteo_Cantidad", table: "Conteo", sql: "[CantidadContada] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Solicitada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Aprobada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_SolicitudDetalle_Entregada", table: "SolicitudDetalle");
            migrationBuilder.DropCheckConstraint(name: "CK_Producto_StockMin", table: "Producto");
            migrationBuilder.DropCheckConstraint(name: "CK_Movimiento_Cantidad", table: "Movimiento");
            migrationBuilder.DropCheckConstraint(name: "CK_Movimiento_Signo", table: "Movimiento");
            migrationBuilder.DropCheckConstraint(name: "CK_Conteo_Cantidad", table: "Conteo");

            migrationBuilder.AlterColumn<decimal>(
                name: "CantidadSolicitada",
                table: "SolicitudDetalle",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CantidadEntregada",
                table: "SolicitudDetalle",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CantidadAprobada",
                table: "SolicitudDetalle",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StockMinimo",
                table: "Producto",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CantidadEfectiva",
                table: "Movimiento",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "Cantidad",
                table: "Movimiento",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "CantidadContada",
                table: "Conteo",
                type: "decimal(18,3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Solicitada", table: "SolicitudDetalle", sql: "[CantidadSolicitada] > 0");
            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Aprobada", table: "SolicitudDetalle", sql: "[CantidadAprobada] IS NULL OR [CantidadAprobada] <= [CantidadSolicitada]");
            migrationBuilder.AddCheckConstraint(name: "CK_SolicitudDetalle_Entregada", table: "SolicitudDetalle", sql: "[CantidadEntregada] >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Producto_StockMin", table: "Producto", sql: "[StockMinimo] >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Movimiento_Cantidad", table: "Movimiento", sql: "[Cantidad] > 0");
            migrationBuilder.AddCheckConstraint(
                name: "CK_Movimiento_Signo",
                table: "Movimiento",
                sql: "([TipoMovimiento] IN (1,3) AND [CantidadEfectiva] = [Cantidad]) OR ([TipoMovimiento] IN (2,4) AND [CantidadEfectiva] = -[Cantidad])");
            migrationBuilder.AddCheckConstraint(name: "CK_Conteo_Cantidad", table: "Conteo", sql: "[CantidadContada] >= 0");
        }
    }
}
