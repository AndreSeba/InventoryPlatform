using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
public class MovimientosController : ControllerBase
{
    private readonly IMovimientoService _movimientoService;

    public MovimientosController(IMovimientoService movimientoService)
    {
        _movimientoService = movimientoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> Listar(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var movimientos = await _movimientoService.ListarAsync(desde, hasta, ct);
        return Ok(movimientos);
    }

    [HttpGet("producto/{productoId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> ListarPorProducto(int productoId, CancellationToken ct)
    {
        var movimientos = await _movimientoService.ListarPorProductoAsync(productoId, ct);
        return Ok(movimientos);
    }

    // Equivalente a vw_PrestamosPendientes de la guía v4.
    [HttpGet("prestamos-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovimientoDto>>> ListarPrestamosPendientes(CancellationToken ct)
    {
        var prestamos = await _movimientoService.ListarPrestamosPendientesAsync(ct);
        return Ok(prestamos);
    }

    [HttpPost("entradas")]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarEntrada([FromBody] RegistrarEntradaDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarEntradaAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("salidas")]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarSalida([FromBody] RegistrarSalidaDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarSalidaAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("ajustes")]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarAjuste([FromBody] RegistrarAjusteDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarAjusteAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(ListarPorProducto), new { productoId = dto.ProductoId }, resultado);
    }

    [HttpPost("devoluciones")]
    [ProducesResponseType(typeof(MovimientoResultadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoResultadoDto>> RegistrarDevolucion([FromBody] RegistrarDevolucionDto dto, CancellationToken ct)
    {
        var resultado = await _movimientoService.RegistrarDevolucionAsync(dto, UsuarioActual(), ct);
        return CreatedAtAction(nameof(Listar), resultado);
    }

    private string UsuarioActual() => User.Identity?.Name ?? "sistema";
}
