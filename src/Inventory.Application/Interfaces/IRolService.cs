using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListarAsync(int paisId, CancellationToken ct);
    Task<RolDto> CrearAsync(CrearRolDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<RolDto> ActualizarAsync(int id, ActualizarRolDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    IReadOnlyList<PermisoDto> ListarPermisosDisponibles();
}
