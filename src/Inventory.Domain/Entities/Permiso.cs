namespace Inventory.Domain.Entities;

public class Permiso
{
    public int Id { get; set; }

    // "modulo.accion", ej. "productos.crear" — es lo que viaja como claim en el JWT
    // y lo que se usa como nombre de policy en los Controllers ([Authorize(Policy=...)]).
    public string Codigo { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty; // agrupa en la UI de roles: "Productos", "Movimientos"...
    public string Descripcion { get; set; } = string.Empty;

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}
