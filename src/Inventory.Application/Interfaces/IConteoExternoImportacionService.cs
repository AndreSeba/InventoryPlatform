using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IConteoExternoImportacionService
{
    Task<VistaPreviaImportacionConteoExternoDto> VistaPreviaAsync(byte[] archivo, CancellationToken ct);

    Task<ImportacionConteoExternoResultadoDto> ImportarAsync(
        byte[] archivo, MapeoColumnasConteoExternoDto mapeo, int paisId, UsuarioActuante usuario, CancellationToken ct);
}
