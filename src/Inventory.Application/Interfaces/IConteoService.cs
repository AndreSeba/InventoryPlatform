using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoService
{
    Task<ConteoDto> RegistrarAsync(RegistrarConteoDto dto, string usuarioId, CancellationToken ct);
    Task<IReadOnlyList<ConteoDto>> ListarPorSesionAsync(string sesionConteo, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo, string SesionConteo)> GenerarHojaConteoAsync(GenerarHojaConteoDto dto, CancellationToken ct);
    Task<ImportarHojaConteoResultadoDto> ImportarHojaConteoAsync(Stream archivo, string usuarioId, CancellationToken ct);
}
