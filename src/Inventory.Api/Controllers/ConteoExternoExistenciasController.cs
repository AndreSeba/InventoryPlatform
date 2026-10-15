using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — existencias propias (Fase 2: alta manual; la
// importación desde Excel es la Fase 5). Nunca el stock calculado del resto del
// sistema (ver CLAUDE.md).
[ApiController]
[Route("api/conteo-externo/existencias")]
[Authorize]
public class ConteoExternoExistenciasController : ControllerBase
{
    private readonly IConteoExternoExistenciaService _existenciaService;

    public ConteoExternoExistenciasController(IConteoExternoExistenciaService existenciaService) => _existenciaService = existenciaService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoExistenciaDto>>> Listar(
        [FromQuery] int? conteoExternoProductoId, [FromQuery] string? almacenCodigo, [FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _existenciaService.ListarAsync(User.ObtenerPaisId(), conteoExternoProductoId, almacenCodigo, incluirInactivas, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoExistenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoExternoExistenciaDto>> Crear([FromBody] CrearConteoExternoExistenciaDto dto, CancellationToken ct)
    {
        var creada = await _existenciaService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoExistenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoExistenciaDto>> Actualizar(int id, [FromBody] ActualizarConteoExternoExistenciaDto dto, CancellationToken ct)
        => Ok(await _existenciaService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
