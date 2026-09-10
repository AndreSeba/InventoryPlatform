using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoService
{
    Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, string usuarioId, CancellationToken ct);
    Task<IReadOnlyList<ConteoDto>> ListarPorSesionAsync(string sesionConteo, CancellationToken ct);
}
