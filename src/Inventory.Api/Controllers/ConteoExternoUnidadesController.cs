using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — catálogo de unidades propio (ver CLAUDE.md). Nunca
// UnidadesController (ese es el catálogo general de Producto/Movimiento).
[ApiController]
[Route("api/conteo-externo/unidades")]
[Authorize]
public class ConteoExternoUnidadesController : ControllerBase
{
    private readonly IConteoExternoUnidadService _unidadService;

    public ConteoExternoUnidadesController(IConteoExternoUnidadService unidadService) => _unidadService = unidadService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoUnidadDto>>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _unidadService.ListarAsync(User.ObtenerPaisId(), incluirInactivas, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoUnidadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoExternoUnidadDto>> Crear([FromBody] CrearConteoExternoUnidadDto dto, CancellationToken ct)
    {
        var creada = await _unidadService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoUnidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoUnidadDto>> Actualizar(int id, [FromBody] ActualizarConteoExternoUnidadDto dto, CancellationToken ct)
        => Ok(await _unidadService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
