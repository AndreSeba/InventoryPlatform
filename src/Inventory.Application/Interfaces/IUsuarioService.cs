using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(int paisId, CancellationToken ct);
    Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, int paisId, CancellationToken ct);
    Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioDto dto, int paisId, CancellationToken ct);
}
