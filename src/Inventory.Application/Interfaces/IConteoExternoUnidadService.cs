using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoUnidadService
{
    Task<IReadOnlyList<ConteoExternoUnidadDto>> ListarAsync(int paisId, bool incluirInactivas, CancellationToken ct);
    Task<ConteoExternoUnidadDto> CrearAsync(CrearConteoExternoUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoExternoUnidadDto> ActualizarAsync(int id, ActualizarConteoExternoUnidadDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
