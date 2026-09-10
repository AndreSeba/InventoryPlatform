using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record MovimientoDto(
    int Id,
    string NumeroMovimiento,
    int ProductoId,
    string ProductoNombre,
    TipoMovimiento TipoMovimiento,
    decimal Cantidad,
    int UbicacionId,
    string UbicacionCodigo,
    bool Retorna,
    string? UbicacionExterna,
    DateOnly? FechaRetornoEsperada,
    int? MovimientoOrigenId,
    int? SolicitudDetalleId,
    string RegistradoPor,
    string? Motivo,
    DateTime FechaMovimiento
);

public record RegistrarEntradaDto(int ProductoId, int UbicacionId, decimal Cantidad, string? Motivo);

public record RegistrarSalidaDto(
    int ProductoId,
    int UbicacionId,
    decimal Cantidad,
    string? Motivo,
    bool Retorna,
    string? UbicacionExterna,
    DateOnly? FechaRetornoEsperada,
    int? SolicitudDetalleId
);

public record RegistrarAjusteDto(int ProductoId, int UbicacionId, decimal Cantidad, bool EsPositivo, string Motivo);

public record RegistrarDevolucionDto(int MovimientoOrigenId, int UbicacionId, decimal Cantidad, string? Motivo);

public record MovimientoResultadoDto(int MovimientoId, string NumeroMovimiento, string Estado, DateTime FechaMovimiento, decimal ExistenciaResultante);
