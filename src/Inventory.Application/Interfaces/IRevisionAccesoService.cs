using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IRevisionAccesoService
{
    Task<IReadOnlyList<RevisionAccesoResumenDto>> ListarAsync(int paisId, CancellationToken ct);
    Task<RevisionAccesoDetalleDto> ObtenerAsync(int id, int paisId, int usuarioId, CancellationToken ct);
    Task<EstadoRevisionesAccesoDto> ObtenerEstadoAsync(int paisId, CancellationToken ct);
    Task<RevisionAccesoDetalleDto> CrearAsync(CrearRevisionAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<RevisionAccesoDetalleDto> DecidirAsync(int id, int lineaId, DecidirAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<RevisionAccesoDetalleDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<RevisionAccesoDetalleDto> CancelarAsync(int id, CancelarRevisionAccesoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo)> GenerarActaAsync(int id, int paisId, CancellationToken ct);
}
