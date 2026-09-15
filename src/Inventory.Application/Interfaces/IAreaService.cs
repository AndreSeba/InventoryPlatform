using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IAreaService
{
    Task<IReadOnlyList<AreaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct);
    Task<AreaDto> CrearAsync(CrearAreaDto dto, CancellationToken ct);
    Task<AreaDto> ActualizarAsync(int id, ActualizarAreaDto dto, CancellationToken ct);
}
