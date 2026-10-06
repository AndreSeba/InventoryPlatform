using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Enums;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Préstamos y avisos de devolución. El solicitante ve lo que tiene prestado y avisa; el operario ve los avisos
// pendientes y registra la entrada desde api/movimientos/devoluciones (con el id del aviso).
[ApiController]
[Route("api/devoluciones")]
[Authorize]
public class DevolucionesController : ControllerBase
{
    private readonly IDevolucionService _service;

    public DevolucionesController(IDevolucionService service) => _service = service;

    // Todos los préstamos pendientes del país, con quién los tiene y si hay un aviso en camino.
    [HttpGet("prestamos")]
    [Authorize(Policy = Permisos.MovimientosVer)]
    public async Task<ActionResult<IReadOnlyList<PrestamoDto>>> ListarPrestamos(CancellationToken ct)
        => Ok(await _service.ListarPrestamosAsync(User.ObtenerPaisId(), null, ct));

    // Solo lo prestado a nombre de quien consulta.
    [HttpGet("prestamos/mios")]
    [Authorize(Policy = Permisos.DevolucionesAvisar)]
    public async Task<ActionResult<IReadOnlyList<PrestamoDto>>> ListarMisPrestamos(CancellationToken ct)
        => Ok(await _service.ListarPrestamosAsync(User.ObtenerPaisId(), User.ObtenerUsuarioActuante().Id, ct));

    // Para el operario: los avisos del país (por defecto, todos; ?estado=1 = solo los pendientes de recibir).
    [HttpGet("avisos")]
    [Authorize(Policy = Permisos.MovimientosDevolucion)]
    public async Task<ActionResult<IReadOnlyList<AvisoDevolucionDto>>> ListarAvisos([FromQuery] EstadoAvisoDevolucion? estado, CancellationToken ct)
        => Ok(await _service.ListarAvisosAsync(User.ObtenerPaisId(), null, estado, ct));

    [HttpGet("avisos/mios")]
    [Authorize(Policy = Permisos.DevolucionesAvisar)]
    public async Task<ActionResult<IReadOnlyList<AvisoDevolucionDto>>> ListarMisAvisos(CancellationToken ct)
        => Ok(await _service.ListarAvisosAsync(User.ObtenerPaisId(), User.ObtenerUsuarioActuante().Id, null, ct));

    [HttpPost("avisos")]
    [Authorize(Policy = Permisos.DevolucionesAvisar)]
    [ProducesResponseType(typeof(AvisoDevolucionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AvisoDevolucionDto>> Avisar([FromBody] CrearAvisoDevolucionDto dto, CancellationToken ct)
    {
        var creado = await _service.AvisarAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(ListarMisAvisos), creado);
    }

    [HttpPost("avisos/{id:int}/cancelar")]
    [Authorize(Policy = Permisos.DevolucionesAvisar)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvisoDevolucionDto>> Cancelar(int id, [FromBody] CancelarAvisoDevolucionDto dto, CancellationToken ct)
        => Ok(await _service.CancelarAvisoAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
