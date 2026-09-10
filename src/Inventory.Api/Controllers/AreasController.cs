using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/areas")]
public class AreasController : ControllerBase
{
    private readonly IAreaService _areaService;

    public AreasController(IAreaService areaService) => _areaService = areaService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AreaDto>>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _areaService.ListarAsync(incluirInactivas, ct));

    [HttpPost]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AreaDto>> Crear([FromBody] CrearAreaDto dto, CancellationToken ct)
    {
        var creada = await _areaService.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), creada);
    }
}
