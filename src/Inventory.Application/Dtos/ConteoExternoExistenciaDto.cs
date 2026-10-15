namespace Inventory.Application.Dtos;

// CostoReferencia viaja solo para mostrar (sale de ConteoExternoProducto.CostoReferencia)
// — nunca se fija por acá. Para cambiar el costo de un producto, usar
// ActualizarConteoExternoProductoDto (catálogo, Fase 1).
// AlmacenNombre también es solo para mostrar (Fase 6) — si el código no tiene nombre
// registrado, viaja igual al código. AlmacenCodigo sigue siendo la identidad real.
public record ConteoExternoExistenciaDto(
    int Id, int ConteoExternoProductoId, string ProductoCodigo, string ProductoNombre, string? UnidadCodigo,
    string AlmacenCodigo, string AlmacenNombre, string? Lote, decimal CantidadSistema, decimal? CostoReferencia,
    int PaisId, bool Activo
);

// UnidadCodigo viaja en la creación SOLO para fijar la unidad del producto si todavía no
// la tiene — una vez fijada, tiene que coincidir siempre; si no coincide, el servicio
// rechaza con UnidadConteoExternoProductoYaFijadaException.
public record CrearConteoExternoExistenciaDto(int ConteoExternoProductoId, string AlmacenCodigo, string? Lote, decimal Cantidad, string UnidadCodigo);

// AlmacenCodigo/Lote/Producto no se editan acá — son la identidad de la línea; para
// corregirlos, desactivar esta existencia y crear una nueva.
public record ActualizarConteoExternoExistenciaDto(decimal Cantidad, bool Activo);
