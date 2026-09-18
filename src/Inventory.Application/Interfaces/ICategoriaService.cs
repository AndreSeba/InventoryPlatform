using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface ICategoriaService
{
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct);
    Task<CategoriaDto> CrearAsync(CrearCategoriaDto dto, int paisId, CancellationToken ct);
    Task<CategoriaDto> ActualizarAsync(int id, ActualizarCategoriaDto dto, int paisId, CancellationToken ct);
}
