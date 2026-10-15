using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — catálogo de productos propio (ver CLAUDE.md). Nunca
// ProductosController (ese es el catálogo general del resto del sistema).
[ApiController]
[Route("api/conteo-externo/productos")]
[Authorize]
public class ConteoExternoProductosController : ControllerBase
{
    private readonly IConteoExternoProductoService _productoService;

    public ConteoExternoProductosController(IConteoExternoProductoService productoService) => _productoService = productoService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoProductoDto>>> Listar(
        [FromQuery] string? texto, [FromQuery] string? familia, [FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await _productoService.ListarAsync(User.ObtenerPaisId(), texto, familia, incluirInactivos, ct));

    // Autocompletado por código o nombre (hasta `max` resultados, 6 por defecto).
    [HttpGet("buscar")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoProductoBusquedaDto>>> Buscar(
        [FromQuery] string? texto, [FromQuery] int max = 6, CancellationToken ct = default)
        => Ok(await _productoService.BuscarAsync(User.ObtenerPaisId(), texto, max, ct));

    // Unidad, costo, almacén habitual y lotes conocidos del producto, para completar el formulario del sobrante.
    [HttpGet("{id:int}/sugerencias")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    [ProducesResponseType(typeof(ConteoExternoSugerenciasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoSugerenciasDto>> ObtenerSugerencias(int id, CancellationToken ct)
        => Ok(await _productoService.ObtenerSugerenciasAsync(id, User.ObtenerPaisId(), ct));

    [HttpGet("familias")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<string>>> ListarFamilias(CancellationToken ct)
        => Ok(await _productoService.ListarFamiliasAsync(User.ObtenerPaisId(), ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoExternoProductoDto>> Crear([FromBody] CrearConteoExternoProductoDto dto, CancellationToken ct)
    {
        var creado = await _productoService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Listar), creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoProductoDto>> Actualizar(int id, [FromBody] ActualizarConteoExternoProductoDto dto, CancellationToken ct)
        => Ok(await _productoService.ActualizarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));
}
