using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

// Módulo de conteo independiente — Fase 3 (sesión + conteo) y Fase 4 (KPIs, ver
// CLAUDE.md).
[ApiController]
[Route("api/conteo-externo/sesiones")]
[Authorize]
public class ConteoExternoSesionesController : ControllerBase
{
    private readonly IConteoExternoSesionService _sesionService;

    public ConteoExternoSesionesController(IConteoExternoSesionService sesionService) => _sesionService = sesionService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoSesionResumenDto>>> Listar(CancellationToken ct)
        => Ok(await _sesionService.ListarAsync(User.ObtenerPaisId(), ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<ConteoExternoSesionDetalleDto>> Obtener(int id, CancellationToken ct)
        => Ok(await _sesionService.ObtenerAsync(id, User.ObtenerPaisId(), ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoSesionResumenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConteoExternoSesionResumenDto>> Crear([FromBody] CrearConteoExternoSesionDto dto, CancellationToken ct)
    {
        var creada = await _sesionService.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("lineas/{lineaId:int}/conteos/{numeroConteo:int}")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarConteo(int lineaId, int numeroConteo, [FromBody] RegistrarConteoExternoDto dto, CancellationToken ct)
    {
        await _sesionService.RegistrarConteoAsync(lineaId, numeroConteo, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return NoContent();
    }

    [HttpPut("lineas/{lineaId:int}/observacion")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarObservacion(int lineaId, [FromBody] ActualizarObservacionConteoExternoDto dto, CancellationToken ct)
    {
        await _sesionService.ActualizarObservacionAsync(lineaId, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/cerrar")]
    [Authorize(Policy = Permisos.ConteoExternoGestionar)]
    [ProducesResponseType(typeof(ConteoExternoSesionResumenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoExternoSesionResumenDto>> Cerrar(int id, CancellationToken ct)
        => Ok(await _sesionService.CerrarAsync(id, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));

    [HttpGet("{id:int}/kpis")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<ConteoExternoSesionKpiDto>> ObtenerKpis(int id, CancellationToken ct)
        => Ok(await _sesionService.ObtenerKpisAsync(id, User.ObtenerPaisId(), ct));

    // Historial multi-sesión — una fila por sesión, para comparar exactitud a lo largo
    // del tiempo (tipo Power BI).
    [HttpGet("kpis/historial")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoExternoSesionKpiDto>>> ObtenerHistorialKpis(CancellationToken ct)
        => Ok(await _sesionService.ObtenerHistorialKpisAsync(User.ObtenerPaisId(), ct));

    [HttpGet("{id:int}/kpis/excel")]
    [Authorize(Policy = Permisos.ConteoExternoVer)]
    public async Task<IActionResult> ExportarKpisExcel(int id, CancellationToken ct)
    {
        var (contenido, nombreArchivo) = await _sesionService.ExportarKpisExcelAsync(id, User.ObtenerPaisId(), ct);
        return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
    }
}
