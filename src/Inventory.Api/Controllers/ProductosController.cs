using Inventory.Api.Services;
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
    private readonly AlmacenamientoImagenesService _almacenamiento;

    public ProductosController(IProductoService productoService, AlmacenamientoImagenesService almacenamiento)
    {
        _productoService = productoService;
        _almacenamiento = almacenamiento;
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

    // Sin policy propia a propósito: la sube tanto quien está creando (ProductosCrear)
    // como quien está editando (ProductosEditar) un producto existente — el endpoint en
    // sí no persiste nada en Producto, solo deja el archivo listo para que el Crear/
    // Actualizar de abajo (esos sí con su policy) lo guarde en ImagenUrl.
    [HttpPost("imagen")]
    [RequestSizeLimit(5_500_000)]
    [ProducesResponseType(typeof(ImagenSubidaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImagenSubidaDto>> SubirImagen(IFormFile archivo, CancellationToken ct)
    {
        if (!User.HasClaim("permiso", Permisos.ProductosCrear) && !User.HasClaim("permiso", Permisos.ProductosEditar))
            return Forbid();

        var rutaRelativa = await _almacenamiento.GuardarImagenProductoAsync(archivo, ct);
        var urlAbsoluta = $"{Request.Scheme}://{Request.Host}{rutaRelativa}";
        return Ok(new ImagenSubidaDto(urlAbsoluta));
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
