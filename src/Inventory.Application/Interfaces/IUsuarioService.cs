using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct);
    Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, CancellationToken ct);
    Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioDto dto, CancellationToken ct);
}
