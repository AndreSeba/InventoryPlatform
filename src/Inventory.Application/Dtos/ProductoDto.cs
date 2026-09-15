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
    // Ruta RELATIVA (no absoluta) calculada al vuelo — "/api/productos/{id}/imagen" si
    // el producto tiene ImagenData, si no null. El frontend le antepone su ApiBaseUrl al
    // renderizar (ver ProductoApiClient.ApiBaseUrl) — nunca queda una URL vieja grabada.
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
    byte[]? ImagenData,
    string? ImagenContentType
);

public record ActualizarProductoDto(
    string Nombre,
    int CategoriaId,
    string UnidadMedida,
    decimal? CostoUnitario,
    decimal StockMinimo,
    string? Detalle,
    // ImagenData null = "no tocar la imagen actual" (así no hay que reenviar los bytes
    // ya guardados solo porque se editó el nombre). Mandar bytes reales = reemplazarla.
    byte[]? ImagenData,
    string? ImagenContentType
);

public record SiguienteCodigoDto(string Codigo);