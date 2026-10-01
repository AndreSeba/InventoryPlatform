using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IProductoService
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(int paisId, int? categoriaId, bool incluirInactivos, CancellationToken ct);
    // Paralelo a ListarAsync, no lo reemplaza — ListarAsync lo siguen usando el picker de
    // Solicitudes/Nueva y la resolución de categoría de Movimientos, que necesitan el
    // catálogo completo. Solo Productos/Index pide esto. Ver PaginaDto.
    Task<PaginaDto<ProductoDto>> ListarPaginadoAsync(int paisId, int? categoriaId, bool incluirInactivos, string? busqueda, int pagina, int tamanoPagina, CancellationToken ct);
    Task<ProductoDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct);
    Task<SiguienteCodigoDto> ObtenerSiguienteCodigoAsync(int categoriaId, int paisId, CancellationToken ct);
    Task<ProductoDto> CrearAsync(CrearProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ProductoDto> ActualizarAsync(int id, ActualizarProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task DesactivarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<(byte[] Datos, string ContentType)> ObtenerImagenAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<UbicacionConExistenciaDto>> ListarUbicacionesConStockAsync(int productoId, CancellationToken ct);
}
