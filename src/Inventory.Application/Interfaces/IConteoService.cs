using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoService
{
    Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task<IReadOnlyList<ConteoDto>> ListarPorSesionAsync(string sesionConteo, int paisId, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo, string SesionConteo)> GenerarHojaConteoAsync(GenerarHojaConteoDto dto, int paisId, CancellationToken ct);
    Task<ImportarHojaConteoResultadoDto> ImportarHojaConteoAsync(Stream archivo, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
