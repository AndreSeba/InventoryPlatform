namespace Inventory.Domain.Entities;

public class Conteo
{
    public int Id { get; set; }
    public string SesionConteo { get; set; } = string.Empty;

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int UbicacionId { get; set; }
    public Ubicacion? Ubicacion { get; set; }

    public int NumeroConteo { get; set; } // 1, 2, 3... permite reconteos
    public decimal CantidadContada { get; set; }
    public string ContadoPor { get; set; } = string.Empty;
    public DateTime FechaConteo { get; set; } = DateTime.UtcNow;
}
