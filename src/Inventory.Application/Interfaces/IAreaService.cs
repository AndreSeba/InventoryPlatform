using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IAreaService
{
    Task<IReadOnlyList<AreaDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct);
    Task<AreaDto> CrearAsync(CrearAreaDto dto, int paisId, CancellationToken ct);
    Task<AreaDto> ActualizarAsync(int id, ActualizarAreaDto dto, int paisId, CancellationToken ct);
}
