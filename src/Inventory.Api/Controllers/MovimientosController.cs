using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
[Authorize]
public class MovimientosController : ControllerBase
{
    private readonly IMovimientoService _movimientoService;

    public MovimientosController(IMovimientoService movimientoService)
    {
        _movimientoService = movimientoService;
    }

    [HttpGet]
    [Authorize(Policy = Permisos.MovimientosVer)]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> Listar(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var movimientos = await _movimientoService.ListarAsync(User.ObtenerPaisId(), desde, hasta, ct);
        return Ok(movimientos);
    }

    [HttpGet("producto/{productoId:int}")]
    [Authorize(Policy = Permisos.MovimientosVer)]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> ListarPorProducto(int productoId, CancellationToken ct)
    {
        var movimientos = await _movimientoService.ListarPorProductoAsync(productoId, User.ObtenerPaisId(), ct);
        return Ok(movimientos);
    }

    // Equivalente a vw_PrestamosPendientes de la guía v4.
    [HttpGet("prestamos-pendientes")]
    [Authorize(Policy = Permisos.MovimientosVer)]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> ListarPrestamosPendientes(CancellationToken ct)
    {
        var prestamos = await _movimientoService.ListarPrestamosPendientesAsync(User.ObtenerPaisId(), ct);
        return Ok(prestamos);
    }

    [HttpPost("hoja")]
    [Authorize(Policy = Permisos.MovimientosVer)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerarExcel([FromBody] GenerarMovimientosExcelDto dto, CancellationToken ct)
    {
        var (contenido, nombreArchivo) = await _movimientoService.GenerarExcelAsync(dto, User.ObtenerPaisId(), ct);
        return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
    }

    [HttpPost("entradas")]
    [Authorize(Policy = Permisos.MovimientosEntrada)]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarEntrada([FromBody] RegistrarEntradaDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarEntradaAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("salidas")]
    [Authorize(Policy = Permisos.MovimientosSalida)]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarSalida([FromBody] RegistrarSalidaDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarSalidaAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("ajustes")]
    [Authorize(Policy = Permisos.MovimientosAjuste)]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarAjuste([FromBody] RegistrarAjusteDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarAjusteAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("devoluciones")]
    [Authorize(Policy = Permisos.MovimientosDevolucion)]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarDevolucion([FromBody] RegistrarDevolucionDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarDevolucionAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(Listar), resultado);
    }

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
