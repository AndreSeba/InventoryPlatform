using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/solicitudes")]
[Authorize]
public class SolicitudesController : ControllerBase
{
    private readonly ISolicitudService _solicitudService;

    public SolicitudesController(ISolicitudService solicitudService) => _solicitudService = solicitudService;

    [HttpGet]
    [Authorize(Policy = Permisos.SolicitudesVer)]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Listar([FromQuery] string? estado, CancellationToken ct)
        => Ok(await _solicitudService.ListarAsync(estado, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.SolicitudesVer)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SolicitudDto>> ObtenerPorId(int id, CancellationToken ct)
        => Ok(await _solicitudService.ObtenerPorIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Crear([FromBody] CrearSolicitudDto dto, CancellationToken ct)
    {
        var creada = await _solicitudService.CrearAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creada.Id }, creada);
    }

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Aprobar(int id, [FromBody] AprobarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.AprobarAsync(id, dto, UsuarioActual(), ct));

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = Permisos.SolicitudesRechazar)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Rechazar(int id, [FromBody] RechazarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.RechazarAsync(id, dto, UsuarioActual(), ct));

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
