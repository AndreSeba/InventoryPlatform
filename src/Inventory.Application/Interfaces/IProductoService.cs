using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IProductoService
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(int? categoriaId, bool incluirInactivos, CancellationToken ct);
    Task<ProductoDto> ObtenerPorIdAsync(int id, CancellationToken ct);
    Task<ProductoDto> CrearAsync(CrearProductoDto dto, string usuarioId, CancellationToken ct);
    Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, string usuarioId, CancellationToken ct);
    Task DesactivarAsync(int id, string usuarioId, CancellationToken ct);
}
