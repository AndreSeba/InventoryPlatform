using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IProductoService
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(int? categoriaId, bool incluirInactivos, CancellationToken ct);
    Task<ProductoDto> ObtenerPorIdAsync(int id, CancellationToken ct);
    Task<SiguienteCodigoDto> ObtenerSiguienteCodigoAsync(int categoriaId, CancellationToken ct);
    Task<ProductoDto> CrearAsync(CrearProductoDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task DesactivarAsync(int id, UsuarioActuante usuario, CancellationToken ct);
}