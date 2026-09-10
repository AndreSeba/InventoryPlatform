using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/solicitudes")]
public class SolicitudesController : ControllerBase
{
    private readonly ISolicitudService _solicitudService;

    public SolicitudesController(ISolicitudService solicitudService) => _solicitudService = solicitudService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Listar([FromQuery] string? estado, CancellationToken ct)
        => Ok(await _solicitudService.ListarAsync(estado, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SolicitudDto>> ObtenerPorId(int id, CancellationToken ct)
        => Ok(await _solicitudService.ObtenerPorIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Crear([FromBody] CrearSolicitudDto dto, CancellationToken ct)
    {
        var creada = await _solicitudService.CrearAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creada.Id }, creada);
    }

    [HttpPost("{id:int}/aprobar")]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Aprobar(int id, [FromBody] AprobarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.AprobarAsync(id, dto, UsuarioActual(), ct));

    [HttpPost("{id:int}/rechazar")]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Rechazar(int id, [FromBody] RechazarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.RechazarAsync(id, dto, UsuarioActual(), ct));

    private string UsuarioActual() => User.Identity?.Name ?? "sistema";
}
