using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IProductoService
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(int paisId, int? categoriaId, bool incluirInactivos, CancellationToken ct);
    Task<ProductoDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct);
    Task<SiguienteCodigoDto> ObtenerSiguienteCodigoAsync(int categoriaId, int paisId, CancellationToken ct);
    Task<ProductoDto> CrearAsync(CrearProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task DesactivarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<(byte[] Datos, string ContentType)> ObtenerImagenAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<UbicacionConExistenciaDto>> ListarUbicacionesConStockAsync(int productoId, CancellationToken ct);
}
