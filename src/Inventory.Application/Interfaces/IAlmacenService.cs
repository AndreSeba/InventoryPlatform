using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IAlmacenService
{
    Task<IReadOnlyList<AlmacenDto>> ListarAsync(int paisId, bool incluirInactivos, CancellationToken ct);
    Task<AlmacenDto> CrearAsync(CrearAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<AlmacenDto> ActualizarAsync(int id, ActualizarAlmacenDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
