using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface ISolicitudService
{
    Task<IReadOnlyList<SolicitudDto>> ListarAsync(int paisId, string? estado, CancellationToken ct);
    Task<IReadOnlyList<SolicitudDto>> ListarMiasAsync(int usuarioId, int paisId, string? estado, CancellationToken ct);
    Task<SolicitudDto> ObtenerPorIdAsync(int id, int paisId, CancellationToken ct);
    Task<SolicitudDto> CrearAsync(CrearSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<SolicitudDto> AprobarAsync(int id, AprobarSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
