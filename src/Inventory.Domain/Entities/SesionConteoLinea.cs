namespace Inventory.Domain.Entities;

// Una fila de la hoja: un (producto, ubicación) donde había stock al crear el conteo.
// ExistenciaSistema es una foto fija de ese momento — la diferencia se calcula contra
// ella, no contra la existencia "viva", para que movimientos posteriores no la distorsionen.
public class SesionConteoLinea
{
    public int Id { get; set; }

    public int SesionConteoId { get; set; }
    public SesionConteo? SesionConteo { get; set; }

    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int UbicacionId { get; set; }
    public Ubicacion? Ubicacion { get; set; }

    public int ExistenciaSistema { get; set; }

    // Null = todavía sin contar (distinto de 0, que es "se contó y no había nada").
    public int? CantidadContada { get; set; }
    public int? ContadoPorId { get; set; }
    public Usuario? ContadoPor { get; set; }
    public string? ContadoPorNombre { get; set; }
    public DateTime? FechaConteo { get; set; }
}
