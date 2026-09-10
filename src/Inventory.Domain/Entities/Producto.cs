namespace Inventory.Domain.Entities;

public class Producto
{
    public int Id { get; set; }

    // Codigo + '-' + UnidadMedida (regla de la guía v4) — único, se genera en el servicio.
    public string ClaveProducto { get; set; } = string.Empty;
    public string CodigoProducto { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public string UnidadMedida { get; set; } = string.Empty; // UNI / CAJA / PQTS
    public decimal? CostoUnitario { get; set; }
    public decimal StockMinimo { get; set; }
    public string? Detalle { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();

    // Nota de diseño: la guía v4 cachea StockActual en la lista (necesario en
    // SharePoint, donde Sum() no delega bien sobre listas grandes). En SQL Server
    // no hace falta — se calcula on-the-fly con SUM(CantidadEfectiva), igual que
    // ya se hacía antes de este cambio. Evita la clase entera de bugs de caché
    // desincronizado que la propia guía v4 documenta como riesgo (nota 2).
}
