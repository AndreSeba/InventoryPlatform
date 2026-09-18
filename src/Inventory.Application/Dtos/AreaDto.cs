namespace Inventory.Application.Dtos;

public record AreaDto(int Id, string CodigoArea, string NombreArea, bool Activo, int PaisId, string PaisNombre);

public record CrearAreaDto(string CodigoArea, string NombreArea);

public record ActualizarAreaDto(string CodigoArea, string NombreArea, bool Activo);
