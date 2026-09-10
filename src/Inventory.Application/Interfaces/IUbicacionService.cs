using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUbicacionService
{
    Task<IReadOnlyList<UbicacionDto>> ListarAsync(bool incluirInactivas, CancellationToken ct);
    Task<UbicacionDto> CrearAsync(CrearUbicacionDto dto, CancellationToken ct);
}
