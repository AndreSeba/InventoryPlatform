using Inventory.Application.Dtos;
using Inventory.Domain.Enums;

namespace Inventory.Application.Interfaces;

public interface IConteoService
{
    Task<IReadOnlyList<ConteoResumenDto>> ListarAsync(EstadoConteo? estado, int paisId, CancellationToken ct);
    Task<ConteoDetalleDto> ObtenerAsync(int id, int paisId, CancellationToken ct);
    Task<ConteoDetalleDto> CrearAsync(CrearConteoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoDetalleDto> GuardarCantidadesAsync(int id, GuardarCantidadesDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo)> GenerarHojaAsync(int id, int paisId, CancellationToken ct);
    Task<ImportarHojaConteoResultadoDto> ImportarHojaAsync(int id, byte[] archivo, string nombreArchivo, bool adjuntarComoEvidencia, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoEvidenciaDto> AdjuntarEvidenciaAsync(int id, string nombreArchivo, byte[] datos, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task EliminarEvidenciaAsync(int id, int evidenciaId, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<(byte[] Datos, string ContentType, string NombreArchivo)> ObtenerEvidenciaAsync(int id, int evidenciaId, int paisId, CancellationToken ct);
    Task<ConteoDetalleDto> CerrarAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoDetalleDto> CancelarAsync(int id, CancelarConteoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<ConteoDetalleDto> CrearReconteoAsync(int id, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
