namespace Inventory.Application.Dtos;

public record PaisDto(int Id, string Nombre, string CodigoIso, bool Activo);

public record CrearPaisDto(string Nombre, string CodigoIso);

public record ActualizarPaisDto(string Nombre, bool Activo);
