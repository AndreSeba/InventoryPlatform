using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record SolicitudDetalleDto(
    int Id, int ProductoId, string ProductoNombre, string ProductoCodigo, string UnidadMedida, decimal? CostoUnitario,
    decimal CantidadSolicitada, decimal? CantidadAprobada, decimal CantidadEntregada
);

public record SolicitudDto(
    int Id, string NumeroSolicitud, int AreaId, string AreaNombre, EstadoSolicitud Estado,
    DateTime FechaSolicitud, int SolicitadoPorId, string SolicitadoPor,
    int? AprobadoPorId, string? AprobadoPor, DateTime? FechaResolucion,
    string? MotivoRechazo, IReadOnlyList<SolicitudDetalleDto> Detalles
);

public record CrearSolicitudDetalleDto(int ProductoId, decimal CantidadSolicitada);

public record CrearSolicitudDto(int AreaId, IReadOnlyList<CrearSolicitudDetalleDto> Detalles);

public record AprobarSolicitudDetalleDto(int SolicitudDetalleId, decimal CantidadAprobada);

public record AprobarSolicitudDto(IReadOnlyList<AprobarSolicitudDetalleDto> Detalles);

public record RechazarSolicitudDto(string MotivoRechazo);
