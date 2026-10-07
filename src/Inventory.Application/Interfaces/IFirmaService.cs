using Inventory.Application.Dtos;

namespace Inventory.Application.Interfaces;

public interface IFirmaService
{
    Task<FirmaPropiaDto> ObtenerPropiaAsync(int usuarioId, CancellationToken ct);
    Task<FirmaPropiaDto> GuardarAsync(GuardarFirmaDto dto, int paisId, UsuarioActuante usuario, CancellationToken ct);
    Task EliminarAsync(int paisId, UsuarioActuante usuario, CancellationToken ct);

    // Las firmas que se estampan en el formulario de una solicitud (el llamador ya validó que puede verla).
    Task<FirmasSolicitudDto> ObtenerDeSolicitudAsync(int solicitudId, int paisId, CancellationToken ct);
}
