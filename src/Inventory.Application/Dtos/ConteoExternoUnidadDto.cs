namespace Inventory.Application.Dtos;

public record ConteoExternoUnidadDto(
    int Id, string Codigo, int DecimalesPermitidos, bool Activo, int PaisId, string PaisNombre
);

public record CrearConteoExternoUnidadDto(string Codigo, int DecimalesPermitidos);

public record ActualizarConteoExternoUnidadDto(int DecimalesPermitidos, bool Activo);
