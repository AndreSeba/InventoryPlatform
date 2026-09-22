using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUnidadService
{
    Task<IReadOnlyList<UnidadDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct);
    Task<UnidadDto> CrearAsync(CrearUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<UnidadDto> ActualizarAsync(int id, ActualizarUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
