using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record UbicacionDto(
    int Id, TipoUbicacion TipoUbicacion, string Nro, string Lado, string? Nivel,
    string CodigoUbicacion, bool Activo, int AlmacenId, string AlmacenNombre, string AlmacenPais
);

public record CrearUbicacionDto(int AlmacenId, TipoUbicacion TipoUbicacion, string Nro, string Lado, string? Nivel);
