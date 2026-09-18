using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record SolicitudDetalleDto(
    int Id, int ProductoId, string ProductoNombre, string ProductoCodigo, string UnidadMedida, decimal? CostoUnitario,
    int CantidadSolicitada, int? CantidadAprobada, int CantidadEntregada,
    bool Retorna, string? UbicacionExterna, DateOnly? FechaRetornoEsperada
);

public record SolicitudDto(
    int Id, string NumeroSolicitud, int AreaId, string AreaNombre, TipoSolicitud Tipo, EstadoSolicitud Estado,
    DateTime FechaSolicitud, int SolicitadoPorId, string SolicitadoPor,
    int? AprobadoPorId, string? AprobadoPor, DateTime? FechaResolucion,
    string? MotivoRechazo, IReadOnlyList<SolicitudDetalleDto> Detalles
);

public record CrearSolicitudDetalleDto(
    int ProductoId, int CantidadSolicitada,
    bool Retorna = false, string? UbicacionExterna = null, DateOnly? FechaRetornoEsperada = null
);

public record CrearSolicitudDto(int AreaId, TipoSolicitud Tipo, IReadOnlyList<CrearSolicitudDetalleDto> Detalles);

// Un grupo por encargado de categoría involucrado en la solicitud — ver
// ISolicitudNotificationService. Dos categorías con el mismo encargado se funden en un
// solo grupo (una sola notificación, con todas sus líneas).
public record EncargadoNotificacionDto(
    int EncargadoId, string EncargadoNombre, string EncargadoEmail, IReadOnlyList<SolicitudDetalleDto> Lineas
);

public record AprobarSolicitudDetalleDto(int SolicitudDetalleId, int CantidadAprobada);

public record AprobarSolicitudDto(IReadOnlyList<AprobarSolicitudDetalleDto> Detalles);

public record RechazarSolicitudDto(string MotivoRechazo);
