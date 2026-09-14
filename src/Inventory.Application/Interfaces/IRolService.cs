using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IRolService
{
    Task<IReadOnlyList<RolDto>> ListarAsync(CancellationToken ct);
    Task<RolDto> CrearAsync(CrearRolDto dto, CancellationToken ct);
    Task<RolDto> ActualizarAsync(int id, ActualizarRolDto dto, CancellationToken ct);
    IReadOnlyList<PermisoDto> ListarPermisosDisponibles();
}
