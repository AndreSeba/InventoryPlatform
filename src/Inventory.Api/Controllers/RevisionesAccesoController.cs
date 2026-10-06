using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/revisiones-acceso")]
[Authorize(Policy = Permisos.AccesosRevisar)]
public class RevisionesAccesoController : ControllerBase
{
    private const string ContentTypeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRevisionAccesoService _service;

    public RevisionesAccesoController(IRevisionAccesoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RevisionAccesoResumenDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(User.ObtenerPaisId(), ct));

    // Cuánto hace de la última revisión cerrada de cada tipo y si ya venció (aviso de Inicio).
    [HttpGet("estado")]
    public async Task<ActionResult<EstadoRevisionesAccesoDto>> Estado(CancellationToken ct)
        => Ok(await _service.ObtenerEstadoAsync(User.ObtenerPaisId(), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevisionAccesoDetalleDto>> Obtener(int id, CancellationToken ct)
        => Ok(await _service.ObtenerAsync(id, User.ObtenerPaisId(), User.ObtenerUsuarioActuante().Id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(RevisionAccesoDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RevisionAccesoDetalleDto>> Crear([FromBody] CrearRevisionAccesoDto dto, CancellationToken ct)
    {
        var creada = await _service.CrearAsync(dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Resumen.Id }, creada);
    }

    [HttpPut("{id:int}/lineas/{lineaId:int}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RevisionAccesoDetalleDto>> Decidir(int id, int lineaId, [FromBody] DecidirAccesoDto dto, CancellationToken ct)
        => Ok(await _service.DecidirAsync(id, lineaId, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));

    [HttpPost("{id:int}/cerrar")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RevisionAccesoDetalleDto>> Cerrar(int id, CancellationToken ct)
        => Ok(await _service.CerrarAsync(id, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));

    [HttpPost("{id:int}/cancelar")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RevisionAccesoDetalleDto>> Cancelar(int id, [FromBody] CancelarRevisionAccesoDto dto, CancellationToken ct)
        => Ok(await _service.CancelarAsync(id, dto, User.ObtenerPaisId(), User.ObtenerUsuarioActuante(), ct));

    [HttpGet("{id:int}/acta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acta(int id, CancellationToken ct)
    {
        var (contenido, nombre) = await _service.GenerarActaAsync(id, User.ObtenerPaisId(), ct);
        return File(contenido, ContentTypeXlsx, nombre);
    }
}
