namespace Inventory.Domain.Entities;

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // A qué país pertenece este rol — asignado automático desde el claim de la sesión
    // al crear (ver RolService), nunca elegido a mano. Cada país tiene sus propios 4
    // roles por defecto (ver RolesPorDefecto/PaisService.CrearAsync).
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
