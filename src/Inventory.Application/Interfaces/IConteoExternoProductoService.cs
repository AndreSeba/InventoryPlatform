using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoProductoService
{
    Task<IReadOnlyList<ConteoExternoProductoDto>> ListarAsync(int paisId, string? texto, string? familia, bool incluirInactivos, CancellationToken ct);
    Task<IReadOnlyList<string>> ListarFamiliasAsync(int paisId, CancellationToken ct);
    Task<ConteoExternoProductoDto> CrearAsync(CrearConteoExternoProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoExternoProductoDto> ActualizarAsync(int id, ActualizarConteoExternoProductoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
