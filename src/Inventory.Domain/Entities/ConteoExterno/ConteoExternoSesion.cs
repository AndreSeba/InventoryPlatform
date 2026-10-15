using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Sesión del módulo de conteo independiente.
public class ConteoExternoSesion
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public EstadoConteoExternoSesion Estado { get; set; } = EstadoConteoExternoSesion.EnCurso;

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public int CreadoPorId { get; set; }
    public Usuario? CreadoPor { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public int? CerradoPorId { get; set; }
    public Usuario? CerradoPor { get; set; }
    public string? CerradoPorNombre { get; set; }
    public DateTime? FechaCierre { get; set; }

    public ICollection<ConteoExternoLinea> Lineas { get; set; } = [];
}
