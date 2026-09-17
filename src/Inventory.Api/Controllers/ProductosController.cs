using Inventory.Api.Security;
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
    // La imagen viaja en el mismo body como base64 (no en un upload aparte) — ~6.7MB para
    // 5MB de imagen real, por la sobrecarga de base64 + el resto del JSON.
    [RequestSizeLimit(8_000_000)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoDto>> Crear([FromBody] CrearProductoDto dto, CancellationToken ct)
    {
        var usuario = UsuarioActual();
        var creado = await _productoService.CrearAsync(dto, usuario, ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
    }

    // Alta rápida desde el carrito de una solicitud de Entrada: el solicitante es quien
    // se entera de un material nuevo (aviso del proveedor/categoría) y necesita poder
    // registrarlo él mismo para no recurrir a texto libre (que generaría productos
    // duplicados con variaciones de nombre) — ver CLAUDE.md. Mismo servicio que el alta
    // de catálogo normal, pero gateado por solicitudes.crear en vez de productos.crear.
    [HttpPost("rapido")]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoDto>> CrearRapido([FromBody] CrearProductoDto dto, CancellationToken ct)
    {
        var usuario = UsuarioActual();
        var creado = await _productoService.CrearAsync(dto, usuario, ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ProductosEditar)]
    [RequestSizeLimit(8_000_000)]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] ActualizarProductoDto dto, CancellationToken ct)
    {
        var usuario = UsuarioActual();
        var actualizado = await _productoService.ActualizarAsync(id, dto, usuario, ct);
        return Ok(actualizado);
    }

    // Sin [Authorize] a propósito: esto se referencia desde un <img src="..."> / CSS
    // background-image del navegador, que nunca manda el header Authorization con el
    // Bearer token — igual que la carpeta /uploads estática que reemplaza. La imagen en sí
    // no es un dato sensible (son fotos de material promocional), así que este es el mismo
    // nivel de exposición que ya tenía el archivo servido por disco.
    [HttpGet("{id:int}/imagen")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerImagen(int id, CancellationToken ct)
    {
        var (datos, contentType) = await _productoService.ObtenerImagenAsync(id, ct);
        return File(datos, contentType);
    }

    // Desactivación lógica (sección 8.1 de la propuesta) — nunca DELETE físico.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permisos.ProductosDesactivar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuario = UsuarioActual();
        await _productoService.DesactivarAsync(id, usuario, ct);
        return NoContent();
    }

    // Usado por Movimientos (Salida/Ajuste negativo) y Conteo físico para no dejar elegir
    // una ubicación donde este producto no tiene nada guardado.
    [HttpGet("{id:int}/ubicaciones")]
    [Authorize(Policy = Permisos.ProductosVer)]
    [ProducesResponseType(typeof(IReadOnlyList<UbicacionConExistenciaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UbicacionConExistenciaDto>>> ListarUbicaciones(int id, CancellationToken ct)
        => Ok(await _productoService.ListarUbicacionesConStockAsync(id, ct));

    [HttpGet("siguiente-codigo")]
    [Authorize(Policy = Permisos.ProductosCrear)]
    [ProducesResponseType(typeof(SiguienteCodigoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiguienteCodigoDto>> ObtenerSiguienteCodigo([FromQuery] int categoriaId, CancellationToken ct)
    {
        var resultado = await _productoService.ObtenerSiguienteCodigoAsync(categoriaId, ct);
        return Ok(resultado);
    }

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
