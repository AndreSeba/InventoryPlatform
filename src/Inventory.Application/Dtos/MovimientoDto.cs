using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record MovimientoDto(
    int Id,
    string NumeroMovimiento,
    int ProductoId,
    string ProductoNombre,
    TipoMovimiento TipoMovimiento,
    int Cantidad,
    int UbicacionId,
    string UbicacionCodigo,
    int AlmacenId,
    string AlmacenNombre,
    bool Retorna,
    string? UbicacionExterna,
    DateOnly? FechaRetornoEsperada,
    int? MovimientoOrigenId,
    int? SolicitudDetalleId,
    int RegistradoPorId,
    string RegistradoPor,
    string? Motivo,
    DateTime FechaMovimiento
);

public record RegistrarEntradaDto(int ProductoId, int UbicacionId, int Cantidad, string? Motivo, int? SolicitudDetalleId = null);

public record RegistrarSalidaDto(
    int ProductoId,
    int UbicacionId,
    int Cantidad,
    string? Motivo,
    bool Retorna,
    string? UbicacionExterna,
    DateOnly? FechaRetornoEsperada,
    int? SolicitudDetalleId
);

public record RegistrarAjusteDto(int ProductoId, int UbicacionId, int Cantidad, bool EsPositivo, string Motivo);

public record RegistrarDevolucionDto(int MovimientoOrigenId, int UbicacionId, int Cantidad, string? Motivo);

public record MovimientoResultadoDto(int MovimientoId, string NumeroMovimiento, string Estado, DateTime FechaMovimiento, int ExistenciaResultante);

public record GenerarMovimientosExcelDto(TipoMovimiento? Tipo, int? CategoriaId);
