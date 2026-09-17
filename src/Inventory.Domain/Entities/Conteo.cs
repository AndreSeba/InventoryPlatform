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
    public int CantidadContada { get; set; }
    public int ContadoPorId { get; set; }
    public Usuario? ContadoPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA,
    // ver CLAUDE.md): el FK de arriba dice QUIÉN fue y sigue siendo válido aunque
    // la persona cambie de nombre; esto dice con qué nombre se firmó entonces.
    public string ContadoPorNombre { get; set; } = string.Empty;
    public DateTime FechaConteo { get; set; } = DateTime.UtcNow;
}
