using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface ISolicitudService
{
    Task<IReadOnlyList<SolicitudDto>> ListarAsync(string? estado, CancellationToken ct);
    Task<IReadOnlyList<SolicitudDto>> ListarMiasAsync(int usuarioId, string? estado, CancellationToken ct);
    Task<SolicitudDto> ObtenerPorIdAsync(int id, CancellationToken ct);
    Task<SolicitudDto> CrearAsync(CrearSolicitudDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<SolicitudDto> AprobarAsync(int id, AprobarSolicitudDto dto, UsuarioActuante usuario, CancellationToken ct);
    Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, UsuarioActuante usuario, CancellationToken ct);
}
