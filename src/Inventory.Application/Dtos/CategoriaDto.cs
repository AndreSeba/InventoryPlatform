namespace Inventory.Application.Dtos;

public record CategoriaDto(int Id, string CodigoCategoria, string? Descripcion, bool Activo);

public record CrearCategoriaDto(string CodigoCategoria, string? Descripcion);

public record ActualizarCategoriaDto(string CodigoCategoria, string? Descripcion, bool Activo);
