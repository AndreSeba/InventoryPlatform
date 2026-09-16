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

// Sin UbicacionId a propósito (2026-09-15, pedido del operario): el operario elige
// PRODUCTOS, no ubicaciones — el sistema arma una fila por cada (producto, ubicación)
// donde ese producto realmente tiene stock (ver ProductoService.ListarUbicacionesConStockAsync).
// ProductoIds es obligatorio y no puede venir vacío (2026-09-16): la hoja se arma SIEMPRE
// sobre una selección explícita, nunca sobre el catálogo entero ni una categoría entera —
// por eso CategoriaId tampoco viaja acá, quedó como filtro de la lista en el frontend.
public record GenerarHojaConteoDto(List<int> ProductoIds);

public record ImportarHojaConteoResultadoDto(
    string SesionConteo, int ProductosContados, IReadOnlyList<ConteoDto> ConDiferencia
);
