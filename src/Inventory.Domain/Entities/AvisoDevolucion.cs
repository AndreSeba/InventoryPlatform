using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

// Aviso de devolución: quien pidió un material que sale en préstamo (Salida con Retorna) avisa que lo va a
// devolver; el operario registra la entrada contra ese aviso. Así cada devolución tiene a las dos personas
// (quien avisó y quien recibió) y el solicitante ve en todo momento qué material tiene prestado.
public class AvisoDevolucion
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty; // DEV-{año}-{id}, generado al crear

    public int PaisId { get; set; }
    public Pais? Pais { get; set; }

    // La Salida (préstamo) que se devuelve.
    public int MovimientoOrigenId { get; set; }
    public Movimiento? MovimientoOrigen { get; set; }

    public int Cantidad { get; set; }
    public string? Notas { get; set; }
    public EstadoAvisoDevolucion Estado { get; set; } = EstadoAvisoDevolucion.Pendiente;

    public int AvisadoPorId { get; set; }
    public Usuario? AvisadoPor { get; set; }
    // Snapshot del nombre al momento de la operación (denormalización DELIBERADA, ver CLAUDE.md).
    public string AvisadoPorNombre { get; set; } = string.Empty;
    public DateTime FechaAviso { get; set; } = DateTime.UtcNow;

    // Se completan al recibir O al cancelar (FechaResolucion = fecha de la resolución).
    public int? ResueltoPorId { get; set; }
    public Usuario? ResueltoPor { get; set; }
    public string? ResueltoPorNombre { get; set; }
    public DateTime? FechaResolucion { get; set; }

    // Solo si Estado = Recibido: lo que realmente llegó (puede ser menos que lo avisado) y la Entrada creada.
    public int? CantidadRecibida { get; set; }
    public int? MovimientoDevolucionId { get; set; }
    public Movimiento? MovimientoDevolucion { get; set; }

    // Obligatorio si Estado = Cancelado.
    public string? MotivoCancelacion { get; set; }
}
