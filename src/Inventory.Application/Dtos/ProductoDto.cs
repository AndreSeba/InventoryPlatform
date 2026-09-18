namespace Inventory.Application.Dtos;

public record ProductoDto(
    int Id,
    string ClaveProducto,
    string CodigoProducto,
    string Nombre,
    int CategoriaId,
    string CategoriaNombre,
    int PaisId,
    string PaisNombre,
    string UnidadMedida,
    decimal? CostoUnitario,
    int StockMinimo,
    string? Detalle,
    // Ruta RELATIVA (no absoluta) calculada al vuelo — "/api/productos/{id}/imagen" si
    // el producto tiene ImagenData, si no null. El frontend le antepone su ApiBaseUrl al
    // renderizar (ver ProductoApiClient.ApiBaseUrl) — nunca queda una URL vieja grabada.
    string? ImagenUrl,
    bool Activo,
    int Existencia,
    // MÍNIMO FechaVencimiento entre las Entradas de este producto que tienen el campo
    // cargado — null si ninguna entrada trae vencimiento. No indica si ESE lote puntual
    // sigue en stock (el sistema no trackea qué lote vació cada Salida), es solo un aviso
    // de "este producto tiene algún lote vencido o por vencer, revisar" (ver ProductoService).
    DateOnly? ProximoVencimiento
);

public record CrearProductoDto(
    //string CodigoProducto,
    string Nombre,
    int CategoriaId,
    string UnidadMedida,
    decimal? CostoUnitario,
    int StockMinimo,
    string? Detalle,
    byte[]? ImagenData,
    string? ImagenContentType
);

public record ActualizarProductoDto(
    string Nombre,
    int CategoriaId,
    string UnidadMedida,
    decimal? CostoUnitario,
    int StockMinimo,
    string? Detalle,
    // ImagenData null = "no tocar la imagen actual" (así no hay que reenviar los bytes
    // ya guardados solo porque se editó el nombre). Mandar bytes reales = reemplazarla.
    byte[]? ImagenData,
    string? ImagenContentType
);

public record SiguienteCodigoDto(string Codigo);

// Usado por Movimientos (Salida/Ajuste negativo) y Conteo físico para no dejar elegir una
// ubicación donde el producto no tiene nada guardado — solo se listan las que tienen
// Existencia > 0 en este momento.
public record UbicacionConExistenciaDto(int UbicacionId, string UbicacionCodigo, int AlmacenId, string AlmacenNombre, int Existencia);