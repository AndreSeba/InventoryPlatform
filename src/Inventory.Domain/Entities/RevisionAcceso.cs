using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Campaña de revisión de accesos: foto de las cuentas activas de un país en un momento dado,
// con una decisión por cuenta (mantener / quitar / cambiar de rol). Al cerrarse se aplican las
// decisiones y la campaña queda inmutable como evidencia (el "acta" que se exporta).
public class RevisionAcceso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // REV-{año}-{id}, generado al crear

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    public AlcanceRevisionAcceso Alcance { get; set; }
    public EstadoRevisionAcceso Estado { get; set; } = EstadoRevisionAcceso.EnCurso;
    public string? Notas { get; set; }

    public int IniciadaPorId { get; set; }
    public Usuario? IniciadaPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA, ver CLAUDE.md).
    public string IniciadaPorNombre { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

    // Se completan al cerrar O al cancelar (FechaCierre = fecha de la resolución).
    public int? CerradaPorId { get; set; }
    public Usuario? CerradaPor { get; set; }
    public string? CerradaPorNombre { get; set; }
    public DateTime? FechaCierre { get; set; }

    // Obligatorio si Estado = Cancelada.
    public string? MotivoCancelacion { get; set; }

    public ICollection<RevisionAccesoLinea> Lineas { get; set; } = new List<RevisionAccesoLinea>();
}

public class RevisionAccesoLinea
{
    public int Id { get; set; }

    public int RevisionAccesoId { get; set; }
    public RevisionAcceso? RevisionAcceso { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    // FOTO FIJA de la cuenta al abrir la campaña: el acta muestra lo que se revisó, aunque
    // después cambie el nombre, el rol o el último ingreso.
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioEmail { get; set; } = string.Empty;
    public int RolId { get; set; }
    public string RolNombre { get; set; } = string.Empty;
    public bool EsPrivilegiado { get; set; }
    public DateTime CuentaCreadaEn { get; set; }
    public DateTime? UltimoLoginEn { get; set; }

    public DecisionAcceso Decision { get; set; } = DecisionAcceso.Pendiente;
    // Solo con Decision = CambiarRol.
    public int? RolNuevoId { get; set; }
    public Rol? RolNuevo { get; set; }
    public string? RolNuevoNombre { get; set; }
    public string? Comentario { get; set; }

    public int? RevisadoPorId { get; set; }
    public Usuario? RevisadoPor { get; set; }
    public string? RevisadoPorNombre { get; set; }
    public DateTime? FechaRevision { get; set; }

    // Se marca al cerrar la campaña, cuando la decisión (quitar / cambiar rol) se aplicó de verdad.
    public bool Aplicada { get; set; }
}
