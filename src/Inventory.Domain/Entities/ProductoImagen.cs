namespace Inventory.Domain.Entities;

// La foto vive en su propia tabla (no en Producto) a propósito: casi toda consulta que
// carga un Producto — Movimientos, Solicitudes, Conteos, listados — lo hace por el nombre
// o el código, nunca por la foto. Con la columna varbinary(max) dentro de Producto, cada
// una de esas consultas arrastraba los ~250 KB de cada foto desde SQL (con ~500 productos,
// ~125 MB por listado). Acá solo se lee cuando se sirve GET /api/productos/{id}/imagen
// o se la reemplaza. Producto.TieneImagen dice si existe sin tener que leerla.
public class ProductoImagen
{
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public byte[] Datos { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
}
