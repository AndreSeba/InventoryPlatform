namespace Inventory.Application.Dtos;

public record ConteoDto(
    int Id, string SesionConteo, int ProductoId, string ProductoNombre,
    int UbicacionId, string UbicacionCodigo, int NumeroConteo,
    decimal CantidadContada, string ContadoPor, DateTime FechaConteo,
    decimal ExistenciaSistema, decimal Diferencia
);

public record RegistrarConteoDto(
    string SesionConteo, int ProductoId, int UbicacionId, int NumeroConteo, decimal CantidadContada
);
