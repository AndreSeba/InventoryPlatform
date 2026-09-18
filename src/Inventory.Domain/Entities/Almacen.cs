using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public class Almacen
{
    public int Id { get; set; }
    public string CodigoAlmacen { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public TipoAlmacen TipoAlmacen { get; set; }

    // Solo si TipoAlmacen = Externo — CK_Almacen_Proveedor lo exige.
    public string? ProveedorNombre { get; set; }
    public string? ProveedorContacto { get; set; }
    public string? ProveedorDireccion { get; set; }

    public bool Activo { get; set; } = true;
}
