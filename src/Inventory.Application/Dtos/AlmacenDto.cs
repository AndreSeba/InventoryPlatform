using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

public record AlmacenDto(
    int Id, string CodigoAlmacen, string Nombre, int PaisId, string PaisNombre, string PaisCodigoIso,
    TipoAlmacen TipoAlmacen, string? ProveedorNombre, string? ProveedorContacto, string? ProveedorDireccion,
    bool Activo
);

// Sin PaisId acá a propósito — se asigna automático desde el país de la sesión
// (ver AlmacenService.CrearAsync), nunca se elige a mano en el alta.
public record CrearAlmacenDto(
    string CodigoAlmacen, string Nombre, TipoAlmacen TipoAlmacen,
    string? ProveedorNombre, string? ProveedorContacto, string? ProveedorDireccion
);

public record ActualizarAlmacenDto(
    string CodigoAlmacen, string Nombre, TipoAlmacen TipoAlmacen,
    string? ProveedorNombre, string? ProveedorContacto, string? ProveedorDireccion, bool Activo
);
