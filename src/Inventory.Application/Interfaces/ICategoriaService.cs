using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface ICategoriaService
{
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct);
    Task<CategoriaDto> CrearAsync(CrearCategoriaDto dto, CancellationToken ct);
}
