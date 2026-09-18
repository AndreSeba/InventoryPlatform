namespace Inventory.Application.Dtos;

public record CategoriaDto(int Id, string CodigoCategoria, string? Descripcion, bool Activo, int? EncargadoId, string? EncargadoNombre, int PaisId, string PaisNombre);

public record CrearCategoriaDto(string CodigoCategoria, string? Descripcion, int? EncargadoId);

public record ActualizarCategoriaDto(string CodigoCategoria, string? Descripcion, bool Activo, int? EncargadoId);
