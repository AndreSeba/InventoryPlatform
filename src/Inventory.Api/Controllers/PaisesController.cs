using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/paises")]
[Authorize]
public class PaisesController : ControllerBase
{
    private readonly IPaisService _paisService;

    public PaisesController(IPaisService paisService) => _paisService = paisService;

    // Sin [Authorize(Policy = Permisos.PaisesVer)] a propósito, con [AllowAnonymous]
    // explícito (mismo criterio que AuthController.Login): hace falta poder listar los
    // países ANTES de loguearse, para poblar el selector del login con bandera.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PaisDto>>> Listar([FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _paisService.ListarAsync(incluirInactivos, ct));

    [HttpPost]
    [Authorize(Policy = Permisos.PaisesCrear)]
    [ProducesResponseType(typeof(PaisDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaisDto>> Crear([FromBody] CrearPaisDto dto, CancellationToken ct)
    {
        var creado = await _paisService.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.PaisesEditar)]
    [ProducesResponseType(typeof(PaisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaisDto>> Actualizar(int id, [FromBody] ActualizarPaisDto dto, CancellationToken ct)
        => Ok(await _paisService.ActualizarAsync(id, dto, ct));
}
