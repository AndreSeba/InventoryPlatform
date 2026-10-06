using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// La campanita. Sin policy de permiso a propósito: cualquier persona autenticada ve SUS notificaciones (el usuario
// y el país salen del token, nunca de la ruta), y cada notificación ya se genera solo para quien corresponde.
[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionService _service;

    public NotificacionesController(INotificacionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<NotificacionesDto>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(User.ObtenerUsuarioActuante().Id, User.ObtenerPaisId(), ct));

    [HttpPost("{id:long}/leida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(long id, CancellationToken ct)
    {
        await _service.MarcarLeidaAsync(id, User.ObtenerUsuarioActuante().Id, ct);
        return NoContent();
    }

    [HttpPost("leidas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarTodasLeidas(CancellationToken ct)
    {
        await _service.MarcarTodasLeidasAsync(User.ObtenerUsuarioActuante().Id, User.ObtenerPaisId(), ct);
        return NoContent();
    }
}
