using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/conteos")]
public class ConteosController : ControllerBase
{
    private readonly IConteoService _conteoService;

    public ConteosController(IConteoService conteoService) => _conteoService = conteoService;

    [HttpGet("sesion/{sesionConteo}")]
    public async Task<ActionResult<IReadOnlyList<ConteoDto>>> ListarPorSesion(string sesionConteo, CancellationToken ct)
        => Ok(await _conteoService.ListarPorSesionAsync(sesionConteo, ct));

    [HttpPost]
    [ProducesResponseType(typeof(ConteoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDto>> Registrar([FromBody] RegistrarConteoDto dto, CancellationToken ct)
    {
        var creado = await _conteoService.RegistrarAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorSesion), new { sesionConteo = creado.SesionConteo }, creado);
    }

    private string UsuarioActual() => User.Identity?.Name ?? "sistema";
}
