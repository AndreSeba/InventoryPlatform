using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Un conteo físico completo: nace al elegir los productos (con una foto fija de la
// existencia de cada línea en ese momento), se cuenta, y se cierra recién cuando hay
// evidencia del documento físico. Cerrado/Cancelado ya no se modifica.
public class SesionConteo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // CONT-{año}-{id}, generado al crear

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public string? Nombre { get; set; }
    public string? Notas { get; set; }
    public EstadoConteo Estado { get; set; } = EstadoConteo.EnCurso;

    public int CreadoPorId { get; set; }
    public Usuario? CreadoPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA, ver CLAUDE.md).
    public string CreadoPorNombre { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Se completan al cerrar O al cancelar (FechaCierre = fecha de la resolución).
    public int? CerradoPorId { get; set; }
    public Usuario? CerradoPor { get; set; }
    public string? CerradoPorNombre { get; set; }
    public DateTime? FechaCierre { get; set; }

    // Obligatorio si Estado = Cancelado.
    public string? MotivoCancelacion { get; set; }

    // Reconteo: apunta al conteo cerrado cuyas diferencias se están volviendo a contar.
    public int? ConteoOrigenId { get; set; }
    public SesionConteo? ConteoOrigen { get; set; }

    public ICollection<SesionConteoLinea> Lineas { get; set; } = new List<SesionConteoLinea>();
    public ICollection<SesionConteoEvidencia> Evidencias { get; set; } = new List<SesionConteoEvidencia>();
}
