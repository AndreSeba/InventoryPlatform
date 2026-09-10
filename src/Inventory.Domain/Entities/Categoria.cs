namespace Inventory.Domain.Entities;

public class Categoria
{
    public int Id { get; set; }
    public string CodigoCategoria { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
