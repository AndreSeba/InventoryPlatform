using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record UbicacionDto(
    int Id, TipoUbicacion TipoUbicacion, string Nro, string Lado, string? Nivel,
    string CodigoUbicacion, bool Activo
);

public record CrearUbicacionDto(TipoUbicacion TipoUbicacion, string Nro, string Lado, string? Nivel);
