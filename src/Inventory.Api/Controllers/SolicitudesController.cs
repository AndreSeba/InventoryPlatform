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
        => Ok(await _solicitudService.ListarAsync(User.ObtenerPaisId(), estado, ct));

    // Para el rol Solicitante (solo solicitudes.crear, sin solicitudes.ver) — la pantalla
    // "Mis solicitudes" del frontend pega acá en vez de a Listar().
    [HttpGet("mias")]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> ListarMias([FromQuery] string? estado, CancellationToken ct)
        => Ok(await _solicitudService.ListarMiasAsync(UsuarioActual().Id, User.ObtenerPaisId(), estado, ct));

    // Sin [Authorize(Policy = SolicitudesVer)] a propósito: alguien con solo
    // solicitudes.crear (rol Solicitante) tiene que poder abrir el detalle de SU PROPIA
    // solicitud (para imprimirla, ver su estado) sin tener permiso para ver las de todos.
    // El chequeo de abajo cubre los dos casos: quien puede ver todas, o el dueño.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SolicitudDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var solicitud = await _solicitudService.ObtenerPorIdAsync(id, User.ObtenerPaisId(), ct);

        var puedeVerTodas = User.HasClaim("permiso", Permisos.SolicitudesVer);
        var esDueño = solicitud.SolicitadoPorId == UsuarioActual().Id;
        if (!puedeVerTodas && !esDueño)
            return Forbid();

        return Ok(solicitud);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Crear([FromBody] CrearSolicitudDto dto, CancellationToken ct)
    {
        var creada = await _solicitudService.CrearAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creada.Id }, creada);
    }

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Aprobar(int id, [FromBody] AprobarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.AprobarAsync(id, dto, User.ObtenerPaisId(), UsuarioActual(), ct));

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = Permisos.SolicitudesRechazar)]
    [ProducesResponseType(typeof(SolicitudDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SolicitudDto>> Rechazar(int id, [FromBody] RechazarSolicitudDto dto, CancellationToken ct)
        => Ok(await _solicitudService.RechazarAsync(id, dto, User.ObtenerPaisId(), UsuarioActual(), ct));

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
