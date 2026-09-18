namespace Inventory.Domain.Entities;

public class Area
{
    public int Id { get; set; }
    public string CodigoArea { get; set; } = string.Empty;
    public string NombreArea { get; set; } = string.Empty;

    // A qué país pertenece esta área solicitante — asignado automático desde el claim
    // de la sesión al crear (ver AreaService), nunca elegido a mano.
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;
}
