namespace Inventory.Domain.Entities;

public class Area
{
    public int Id { get; set; }
    public string CodigoArea { get; set; } = string.Empty;
    public string NombreArea { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
