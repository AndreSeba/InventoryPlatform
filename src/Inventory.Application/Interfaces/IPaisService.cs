using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IPaisService
{
    // Sin filtro de país (obvio: Pais ES la dimensión país) — Listar es además el único
    // endpoint [AllowAnonymous] de este servicio, para poblar el selector del login antes
    // de tener sesión.
    Task<IReadOnlyList<PaisDto>> ListarAsync(bool incluirInactivos, CancellationToken ct);
    // paisId acá es el país de la SESIÓN de quien actúa (para escopear la auditoría),
    // no un filtro de datos — Pais no tiene PaisId propio (ver comentario de ListarAsync).
    Task<PaisDto> CrearAsync(CrearPaisDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<PaisDto> ActualizarAsync(int id, ActualizarPaisDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
