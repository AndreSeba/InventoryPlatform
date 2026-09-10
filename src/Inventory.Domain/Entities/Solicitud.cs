using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

public class Solicitud
{
    public int Id { get; set; }
    public string NumeroSolicitud { get; set; } = string.Empty; // SOL-{año}-{id}, generado al crear

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public string SolicitadoPor { get; set; } = string.Empty;
    public string? AprobadoPor { get; set; }
    public DateTime? FechaResolucion { get; set; }

    // Obligatorio si Estado = Rechazada (CK_Solicitud_Aprobacion de la guía v4).
    public string? MotivoRechazo { get; set; }

    public ICollection<SolicitudDetalle> Detalles { get; set; } = new List<SolicitudDetalle>();
}
