using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoExistenciaService
{
    Task<IReadOnlyList<ConteoExternoExistenciaDto>> ListarAsync(int paisId, int? conteoExternoProductoId, string? almacenCodigo, bool incluirInactivas, CancellationToken ct);
    Task<ConteoExternoExistenciaDto> CrearAsync(CrearConteoExternoExistenciaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoExternoExistenciaDto> ActualizarAsync(int id, ActualizarConteoExternoExistenciaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
