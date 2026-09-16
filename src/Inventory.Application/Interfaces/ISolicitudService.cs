using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface ISolicitudService
{
    Task<IReadOnlyList<SolicitudDto>> ListarAsync(string? estado, CancellationToken ct);
    Task<IReadOnlyList<SolicitudDto>> ListarMiasAsync(string usuarioId, string? estado, CancellationToken ct);
    Task<SolicitudDto> ObtenerPorIdAsync(int id, CancellationToken ct);
    Task<SolicitudDto> CrearAsync(CrearSolicitudDto dto, string usuarioId, CancellationToken ct);
    Task<SolicitudDto> AprobarAsync(int id, AprobarSolicitudDto dto, string usuarioId, CancellationToken ct);
    Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, string usuarioId, CancellationToken ct);
}
