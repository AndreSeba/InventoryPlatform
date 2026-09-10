namespace Inventory.Application.Dtos;

public record AreaDto(int Id, string CodigoArea, string NombreArea, bool Activo);

public record CrearAreaDto(string CodigoArea, string NombreArea);
