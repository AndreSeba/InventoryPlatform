namespace Inventory.Application.Dtos;

public record UnidadDto(int Id, string CodigoUnidad, string Nombre, bool Activo, int PaisId, string PaisNombre);

public record CrearUnidadDto(string CodigoUnidad, string Nombre);

// Sin CodigoUnidad a propósito: el código es inmutable porque viaja dentro de
// Producto.ClaveProducto (ver Unidad.cs). "Eliminar" es Activo = false, igual que
// en Categoría y Área.
public record ActualizarUnidadDto(string Nombre, bool Activo);
