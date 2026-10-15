using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — nombres de almacén (Fase 6, ver CLAUDE.md). Nunca
// AlmacenesController (ese es el catálogo general del resto del sistema).
[ApiController]
[Route("api/conteo-externo/almacenes")]
[Authorize]
public class ConteoExternoAlmacenesController : ControllerBase
{
    private readonly IConteoExternoAlmacenService _almacenService;

    public ConteoExternoAlmacenesController(IConteoExternoAlmacenService almacenService) => _almacenService = almacenService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoAlmacenDto>>> Listar([FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _almacenService.ListarAsync(User.ObtenerPaisId(), incluirInactivos, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoAlmacenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoExternoAlmacenDto>> Crear([FromBody] CrearConteoExternoAlmacenDto dto, CancellationToken ct)
    {
        var creado = await _almacenService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoAlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoAlmacenDto>> Actualizar(int id, [FromBody] ActualizarConteoExternoAlmacenDto dto, CancellationToken ct)
        => Ok(await _almacenService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
