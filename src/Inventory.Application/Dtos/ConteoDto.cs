using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record ConteoResumenDto(
    int Id, string Codigo, string? Nombre, EstadoConteo Estado,
    DateTime FechaCreacion, string CreadoPor, DateTime? FechaCierre, string? CerradoPor,
    int TotalLineas, int LineasContadas, int LineasConDiferencia, int CantidadEvidencias,
    int? ConteoOrigenId, string? ConteoOrigenCodigo
);

// Diferencia = CantidadContada - ExistenciaSistema (foto fija de cuando se creó el conteo).
// CantidadContada y Diferencia son null mientras la línea no se contó.
public record ConteoLineaDto(
    int Id, int ProductoId, string ProductoCodigo, string ProductoNombre, string Categoria, string Unidad,
    string? ImagenUrl, int UbicacionId, string UbicacionCodigo, int ExistenciaSistema,
    int? CantidadContada, int? Diferencia, string? ContadoPor, DateTime? FechaConteo
);

public record ConteoEvidenciaDto(int Id, string NombreArchivo, string ContentType, long TamanoBytes, string SubidoPor, DateTime FechaSubida);

public record ConteoDetalleDto(
    ConteoResumenDto Resumen, string? Notas, string? MotivoCancelacion,
    IReadOnlyList<ConteoLineaDto> Lineas, IReadOnlyList<ConteoEvidenciaDto> Evidencias
);

// ProductoIds obligatorio y nunca vacío: la hoja se arma SIEMPRE sobre una selección
// explícita, nunca sobre el catálogo entero ni una categoría entera. Sin UbicacionId: el
// sistema arma una línea por cada (producto, ubicación) donde ese producto tiene stock.
public record CrearConteoDto(string? Nombre, string? Notas, List<int> ProductoIds);

// Cantidad null = dejar la línea otra vez sin contar.
public record CantidadLineaDto(int LineaId, int? Cantidad);
public record GuardarCantidadesDto(List<CantidadLineaDto> Cantidades);

public record CancelarConteoDto(string Motivo);

public record ImportarHojaConteoResultadoDto(int LineasActualizadas, bool EvidenciaAdjuntada);
