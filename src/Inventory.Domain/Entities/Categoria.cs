namespace Inventory.Domain.Entities;

public class Categoria
{
    public int Id { get; set; }
    public string CodigoCategoria { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // A qué país pertenece esta categoría — asignado automático desde el claim
    // de la sesión al crear (ver CategoriaService), nunca elegido a mano.
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public bool Activo { get; set; } = true;

    // Persona de almacén responsable de las solicitudes de productos de esta categoría —
    // a ella se avisa al crearse una Solicitud (ver ISolicitudNotificationService). No
    // restringe quién puede aprobar, solo a quién se le avisa.
    public int? EncargadoId { get; set; }
    public Usuario? Encargado { get; set; }
}
