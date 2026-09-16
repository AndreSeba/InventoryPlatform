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

// Sin UbicacionId a propósito (2026-09-15, pedido del operario): el operario elige
// PRODUCTOS, no ubicaciones — el sistema arma una fila por cada (producto, ubicación)
// donde ese producto realmente tiene stock (ver ProductoService.ListarUbicacionesConStockAsync).
public record GenerarHojaConteoDto(int? CategoriaId, List<int>? ProductoIds);

public record ImportarHojaConteoResultadoDto(
    string SesionConteo, int ProductosContados, IReadOnlyList<ConteoDto> ConDiferencia
);
