using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    [HttpGet]
    [Authorize(Policy = Permisos.ProductosVer)]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductoDto>>> Listar(
        [FromQuery] int? categoriaId, [FromQuery] bool incluirInactivos, CancellationToken ct)
    {
        var productos = await _productoService.ListarAsync(categoriaId, incluirInactivos, ct);
        return Ok(productos);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ProductosVer)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var producto = await _productoService.ObtenerPorIdAsync(id, ct);
        return Ok(producto);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.ProductosCrear)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoDto>> Crear([FromBody] CrearProductoDto dto, CancellationToken ct)
    {
        var usuarioId = UsuarioActual();
        var creado = await _productoService.CrearAsync(dto, usuarioId, ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ProductosEditar)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] ActualizarProductoDto dto, CancellationToken ct)
    {
        var usuarioId = UsuarioActual();
        var actualizado = await _productoService.ActualizarAsync(id, dto, usuarioId, ct);
        return Ok(actualizado);
    }

    // Desactivación lógica (sección 8.1 de la propuesta) — nunca DELETE físico.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permisos.ProductosDesactivar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuarioId = UsuarioActual();
        await _productoService.DesactivarAsync(id, usuarioId, ct);
        return NoContent();
    }

    private string UsuarioActual() => User.Identity?.Name ?? "sistema";
}
