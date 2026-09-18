using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUbicacionService
{
    Task<IReadOnlyList<UbicacionDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct);
    Task<UbicacionDto> CrearAsync(CrearUbicacionDto dto, int paisId, CancellationToken ct);
}
