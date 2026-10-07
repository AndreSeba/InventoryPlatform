using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public class Solicitud
{
    public int Id { get; set; }
    public string NumeroSolicitud { get; set; } = string.Empty; // SOL-{año}-{id}, generado al crear

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    public TipoSolicitud Tipo { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public int SolicitadoPorId { get; set; }
    public Usuario? SolicitadoPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA,
    // ver CLAUDE.md): el FK de arriba dice QUIÉN fue y sigue siendo válido aunque
    // la persona cambie de nombre; esto dice con qué nombre se firmó entonces.
    public string SolicitadoPorNombre { get; set; } = string.Empty;

    // Null hasta que se aprueba o rechaza.
    public int? AprobadoPorId { get; set; }
    public Usuario? AprobadoPor { get; set; }
    public string? AprobadoPorNombre { get; set; }
    public DateTime? FechaResolucion { get; set; }

    // Obligatorio si Estado = Rechazada (CK_Solicitud_Aprobacion de la guía v4).
    public string? MotivoRechazo { get; set; }

    // Versión de la firma manuscrita (FirmaUsuario) con la que se firmó cada punta, fijada al crear / al aprobar. Null si esa
    // persona todavía no tenía firma: en ese caso el formulario usa su firma activa actual (la acción igual la hizo ella).
    public int? FirmaSolicitanteId { get; set; }
    public int? FirmaAprobadorId { get; set; }

    public ICollection<SolicitudDetalle> Detalles { get; set; } = new List<SolicitudDetalle>();
}
