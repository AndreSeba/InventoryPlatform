using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

// Un préstamo = una Salida con Retorna que todavía no volvió por completo. Pendiente = Prestado - Devuelto;
// EnAviso = lo que ya se avisó que se va a devolver y el operario todavía no recibió (parte del Pendiente).
// SolicitadoPor es quien pidió el material: la persona que lo tiene y la que puede avisar su devolución.
public record PrestamoDto(
    int MovimientoId, string NumeroMovimiento, int ProductoId, string ProductoCodigo, string ProductoNombre,
    int Prestado, int Devuelto, int EnAviso, int Pendiente,
    string? UbicacionExterna, DateOnly? FechaRetornoEsperada, int DiasMora, DateTime FechaSalida,
    int? SolicitudId, string? NumeroSolicitud, int? SolicitadoPorId, string? SolicitadoPor
);

public record AvisoDevolucionDto(
    int Id, string Codigo, EstadoAvisoDevolucion Estado,
    int MovimientoOrigenId, string NumeroMovimientoOrigen, string ProductoNombre, string? NumeroSolicitud,
    int Cantidad, string? Notas, int AvisadoPorId, string AvisadoPor, DateTime FechaAviso,
    DateTime? FechaResolucion, string? ResueltoPor, int? CantidadRecibida, string? NumeroMovimientoDevolucion,
    string? MotivoCancelacion
);

public record CrearAvisoDevolucionDto(int MovimientoOrigenId, int Cantidad, string? Notas);

public record CancelarAvisoDevolucionDto(string Motivo);
