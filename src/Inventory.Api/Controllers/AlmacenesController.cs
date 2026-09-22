using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/almacenes")]
[Authorize]
public class AlmacenesController : ControllerBase
{
    private readonly IAlmacenService _almacenService;

    public AlmacenesController(IAlmacenService almacenService) => _almacenService = almacenService;

    [HttpGet]
    [Authorize(Policy = Permisos.AlmacenesVer)]
    public async Task<ActionResult<IReadOnlyList<AlmacenDto>>> Listar([FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _almacenService.ListarAsync(User.ObtenerPaisId(), incluirInactivos, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.AlmacenesCrear)]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AlmacenDto>> Crear([FromBody] CrearAlmacenDto dto, CancellationToken ct)
    {
        var creado = await _almacenService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.AlmacenesEditar)]
    [ProducesResponseType(typeof(AlmacenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AlmacenDto>> Actualizar(int id, [FromBody] ActualizarAlmacenDto dto, CancellationToken ct)
        => Ok(await _almacenService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
