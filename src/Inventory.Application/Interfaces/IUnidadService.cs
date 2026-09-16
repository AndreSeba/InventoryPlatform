using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUnidadService
{
    Task<IReadOnlyList<UnidadDto>> ListarAsync(bool incluirInactivas, CancellationToken ct);
    Task<UnidadDto> CrearAsync(CrearUnidadDto dto, CancellationToken ct);
    Task<UnidadDto> ActualizarAsync(int id, ActualizarUnidadDto dto, CancellationToken ct);
}
