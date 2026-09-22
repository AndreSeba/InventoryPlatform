using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/unidades")]
[Authorize]
public class UnidadesController : ControllerBase
{
    private readonly IUnidadService _unidadService;

    public UnidadesController(IUnidadService unidadService) => _unidadService = unidadService;

    [HttpGet]
    [Authorize(Policy = Permisos.UnidadesVer)]
    public async Task<ActionResult<IReadOnlyList<UnidadDto>>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _unidadService.ListarAsync(User.ObtenerPaisId(), incluirInactivas, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.UnidadesCrear)]
    [ProducesResponseType(typeof(UnidadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UnidadDto>> Crear([FromBody] CrearUnidadDto dto, CancellationToken ct)
    {
        var creada = await _unidadService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.UnidadesEditar)]
    [ProducesResponseType(typeof(UnidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnidadDto>> Actualizar(int id, [FromBody] ActualizarUnidadDto dto, CancellationToken ct)
        => Ok(await _unidadService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
