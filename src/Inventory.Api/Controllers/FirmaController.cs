using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// La firma manuscrita de la persona logueada. Sin policy de permiso a propósito: cada quien gestiona SOLO la suya
// (el usuario sale del token, nunca de la ruta).
[ApiController]
[Route("api/firma")]
[Authorize]
public class FirmaController : ControllerBase
{
    private readonly IFirmaService _service;

    public FirmaController(IFirmaService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<FirmaPropiaDto>> Obtener(CancellationToken ct)
        => Ok(await _service.ObtenerPropiaAsync(User.ObtenerUsuarioActuante().Id, ct));

    [HttpPut]
    [ProducesResponseType(typeof(FirmaPropiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FirmaPropiaDto>> Guardar([FromBody] GuardarFirmaDto dto, CancellationToken ct)
        => Ok(await _service.GuardarAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Eliminar(CancellationToken ct)
    {
        await _service.EliminarAsync(User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return NoContent();
    }
}
