namespace Inventory.Application.Dtos;

public record ConteoDto(
    int Id, string SesionConteo, int ProductoId, string ProductoNombre,
    int UbicacionId, string UbicacionCodigo, int NumeroConteo,
    decimal CantidadContada, int ContadoPorId, string ContadoPor, DateTime FechaConteo,
    decimal ExistenciaSistema, decimal Diferencia
);

public record RegistrarConteoDto(
    string SesionConteo, int ProductoId, int UbicacionId, int NumeroConteo, decimal CantidadContada
);

// ProductoIds es obligatorio y no puede venir vacío: la hoja de conteo se arma SIEMPRE
// sobre una selección explícita de productos, nunca sobre el catálogo entero.
public record GenerarHojaConteoDto(int UbicacionId, List<int> ProductoIds);

public record ImportarHojaConteoResultadoDto(
    string SesionConteo, int ProductosContados, IReadOnlyList<ConteoDto> ConDiferencia
);
