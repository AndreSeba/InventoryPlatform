namespace Inventory.Application.Dtos;

public record ProductoDto(
    int Id,
    string ClaveProducto,
    string CodigoProducto,
    string Nombre,
    int CategoriaId,
    string CategoriaNombre,
    string UnidadMedida,
    decimal? CostoUnitario,
    decimal StockMinimo,
    string? Detalle,
    string? ImagenUrl,
    bool Activo,
    decimal Existencia
);

public record CrearProductoDto(
    //string CodigoProducto,
    string Nombre,
    int CategoriaId,
    string UnidadMedida,
    decimal? CostoUnitario,
    decimal StockMinimo,
    string? Detalle,
    string? ImagenUrl
);

public record ActualizarProductoDto(
    string Nombre,
    int CategoriaId,
    string UnidadMedida,
    decimal? CostoUnitario,
    decimal StockMinimo,
    string? Detalle,
    string? ImagenUrl
);

public record ImagenSubidaDto(string Url);
public record SiguienteCodigoDto(string Codigo);