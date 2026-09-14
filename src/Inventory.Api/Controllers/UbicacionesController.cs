using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/ubicaciones")]
[Authorize]
public class UbicacionesController : ControllerBase
{
    private readonly IUbicacionService _ubicacionService;

    public UbicacionesController(IUbicacionService ubicacionService) => _ubicacionService = ubicacionService;

    [HttpGet]
    [Authorize(Policy = Permisos.UbicacionesVer)]
    public async Task<ActionResult<IReadOnlyList<UbicacionDto>>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _ubicacionService.ListarAsync(incluirInactivas, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.UbicacionesCrear)]
    [ProducesResponseType(typeof(UbicacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UbicacionDto>> Crear([FromBody] CrearUbicacionDto dto, CancellationToken ct)
    {
        var creada = await _ubicacionService.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), creada);
    }
}
