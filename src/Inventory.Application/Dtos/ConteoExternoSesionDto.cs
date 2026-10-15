using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record ConteoExternoSesionResumenDto(
    int Id, string Nombre, EstadoConteoExternoSesion Estado, int PaisId,
    string CreadoPorNombre, DateTime CreadoEn,
    string? CerradoPorNombre, DateTime? FechaCierre,
    int TotalLineas, int LineasContadas
);

public record ConteoExternoRegistroDto(int NumeroConteo, string Ubicacion, decimal Cantidad);

// AlmacenNombre es solo para mostrar (Fase 6) — AlmacenCodigo sigue siendo la foto fija
// real, nunca se reemplaza.
public record ConteoExternoLineaDto(
    int Id, int ConteoExternoProductoId, string ProductoCodigo, string ProductoNombre, string Familia,
    string AlmacenCodigo, string AlmacenNombre, string? Lote, string UnidadCodigo, decimal ExistenciaSistema, decimal CostoUnitario,
    string? Observacion,
    int? ConteoVigente, decimal? TotalContado, decimal? Diferencia, decimal? DiferenciaValor,
    List<ConteoExternoRegistroDto> Registros
);

public record ConteoExternoSesionDetalleDto(
    int Id, string Nombre, EstadoConteoExternoSesion Estado,
    string CreadoPorNombre, DateTime CreadoEn,
    string? CerradoPorNombre, DateTime? FechaCierre,
    List<ConteoExternoLineaDto> Lineas
);

// Selección por criterio (familia/almacén), no producto por producto — a propósito:
// esto es inventario importado de un sistema externo por lote/almacén, se cuenta por
// criterio, no a mano producto por producto (al revés del conteo físico general del
// resto del sistema). Null = sin filtrar por ese criterio.
public record CrearConteoExternoSesionDto(string Nombre, string? Familia, string? AlmacenCodigo);

// Cantidades por ubicación para UN número de conteo de UNA línea. null = sin contar
// (se borra si ya había algo cargado ahí) — el conteo completo de ese número se
// reemplaza con lo que venga en este diccionario (ver ConteoExternoSesionService.
// RegistrarConteoAsync).
public record RegistrarConteoExternoDto(Dictionary<string, decimal?> Cantidades);

public record ActualizarObservacionConteoExternoDto(string? Observacion);

public record ConteoExternoSesionKpiDto(
    int SesionId, string SesionNombre, EstadoConteoExternoSesion Estado, DateTime CreadoEn, DateTime? FechaCierre,
    int LotesInventariados, int LotesCorrectos, decimal? IraLotes,
    int CodigosInventariados, int CodigosCorrectos, decimal? IraCodigos,
    decimal ValorInventario, decimal DiferenciaNetaBob, decimal DiferenciaAbsolutaBob,
    decimal? ExactitudValor, decimal? ExactitudValorAbsoluta,
    int SinContar
);
