using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/conteos")]
[Authorize]
public class ConteosController : ControllerBase
{
    private readonly IConteoService _conteoService;

    public ConteosController(IConteoService conteoService) => _conteoService = conteoService;

    [HttpGet("sesion/{sesionConteo}")]
    [Authorize(Policy = Permisos.ConteosVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoDto>>> ListarPorSesion(string sesionConteo, CancellationToken ct)
        => Ok(await _conteoService.ListarPorSesionAsync(sesionConteo, User.ObtenerPaisId(), ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(typeof(ConteoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDto>> Registrar([FromBody] RegistrarConteoDto dto, CancellationToken ct)
    {
        var creado = await _conteoService.RegistrarAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorSesion), new { sesionConteo = creado.SesionConteo }, creado);
    }

    [HttpPost("hoja")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerarHoja([FromBody] GenerarHojaConteoDto dto, CancellationToken ct)
    {
        var (contenido, nombreArchivo, _) = await _conteoService.GenerarHojaConteoAsync(dto, User.ObtenerPaisId(), ct);
        return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
    }

    [HttpPost("importar")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [RequestSizeLimit(10_000_000)]
    [ProducesResponseType(typeof(ImportarHojaConteoResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportarHojaConteoResultadoDto>> ImportarHoja(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new ProblemDetails { Detail = "Subí el archivo de la hoja de conteo." });

        await using var stream = archivo.OpenReadStream();
        var resultado = await _conteoService.ImportarHojaConteoAsync(stream, User.ObtenerPaisId(), UsuarioActual(), ct);
        return Ok(resultado);
    }

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
