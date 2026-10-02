using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    // Saca la foto de Producto a su propia tabla para que ninguna consulta que carga un
    // Producto arrastre los bytes (ver comentario en ProductoImagen). EF generó esto como
    // DropColumn + CreateTable, que PIERDE las fotos — acá se reordena para copiar primero.
    public partial class SepararImagenDeProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TieneImagen",
                table: "Producto",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProductoImagen",
                columns: table => new
                {
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Datos = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoImagen", x => x.ProductoId);
                    table.ForeignKey(
                        name: "FK_ProductoImagen_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copia de datos ANTES de borrar las columnas. Si ImagenContentType venía
            // vacío (no debería: el servicio lo exige junto con los bytes) se cae a un
            // tipo genérico en vez de violar el NOT NULL de la tabla nueva.
            migrationBuilder.Sql(@"
                INSERT INTO ProductoImagen (ProductoId, Datos, ContentType)
                SELECT Id, ImagenData, ISNULL(ImagenContentType, 'application/octet-stream')
                FROM Producto
                WHERE ImagenData IS NOT NULL;");

            migrationBuilder.Sql("UPDATE Producto SET TieneImagen = 1 WHERE ImagenData IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "ImagenContentType",
                table: "Producto");

            migrationBuilder.DropColumn(
                name: "ImagenData",
                table: "Producto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagenContentType",
                table: "Producto",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImagenData",
                table: "Producto",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.ImagenData = i.Datos, p.ImagenContentType = i.ContentType
                FROM Producto p
                INNER JOIN ProductoImagen i ON i.ProductoId = p.Id;");

            migrationBuilder.DropTable(
                name: "ProductoImagen");

            migrationBuilder.DropColumn(
                name: "TieneImagen",
                table: "Producto");
        }
    }
}
