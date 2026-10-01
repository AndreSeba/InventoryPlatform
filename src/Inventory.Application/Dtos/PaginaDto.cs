namespace Inventory.Application.Dtos;

// Deviación deliberada del patrón del resto del proyecto (Movimientos/Solicitudes/
// Auditoria devuelven la lista filtrada completa y el frontend pagina en memoria con
// <Pager/>). El catálogo de Productos pasó de unas pocas decenas a ~600+ filas tras las
// cargas masivas de inventario, y cada fila dispara además dos agregados (existencia,
// próximo vencimiento) contra Movimiento — traer todo de una se volvió lento. Solo este
// endpoint pagina de verdad en SQL; el resto del proyecto sigue con el patrón de siempre.
public record PaginaDto<T>(IReadOnlyList<T> Items, int Total);
