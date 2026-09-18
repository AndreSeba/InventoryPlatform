using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IPaisService
{
    // Sin filtro de país (obvio: Pais ES la dimensión país) — Listar es además el único
    // endpoint [AllowAnonymous] de este servicio, para poblar el selector del login antes
    // de tener sesión.
    Task<IReadOnlyList<PaisDto>> ListarAsync(bool incluirInactivos, CancellationToken ct);
    Task<PaisDto> CrearAsync(CrearPaisDto dto, CancellationToken ct);
    Task<PaisDto> ActualizarAsync(int id, ActualizarPaisDto dto, CancellationToken ct);
}
