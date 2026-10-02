namespace Inventory.Application.Dtos;

// Todo lo que muestra la pantalla de Inicio, ya agregado en la base. Antes el frontend
// bajaba el catálogo entero y TODOS los movimientos solo para contar "bajo mínimo" y
// mostrar cinco filas — con cientos de productos eso hacía lenta la pantalla de entrada.
public record ResumenInicioDto(
    int BajoMinimo,
    int PrestamosPendientes,
    int ItemsTotales,
    int SolicitudesDelMes,
    IReadOnlyList<MovimientoDto> Recientes
);
