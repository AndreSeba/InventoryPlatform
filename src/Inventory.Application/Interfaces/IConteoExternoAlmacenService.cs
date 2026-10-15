using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoAlmacenService
{
    Task<IReadOnlyList<ConteoExternoAlmacenDto>> ListarAsync(int paisId, bool incluirInactivos, CancellationToken ct);
    Task<ConteoExternoAlmacenDto> CrearAsync(CrearConteoExternoAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoExternoAlmacenDto> ActualizarAsync(int id, ActualizarConteoExternoAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
