using Inventory.Api.Security;
using Inventory.Application.Dtos;
using Inventory.Application.Exceptions;
using Inventory.Application.Interfaces;
using Inventory.Domain.Enums;
using Inventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/conteos")]
[Authorize]
public class ConteosController : ControllerBase
{
    private const long MaxBytesArchivo = 10 * 1024 * 1024;
    private const string ContentTypeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IConteoService _conteoService;

    public ConteosController(IConteoService conteoService) => _conteoService = conteoService;

    [HttpGet]
    [Authorize(Policy = Permisos.ConteosVer)]
    public async Task<ActionResult<IReadOnlyList<ConteoResumenDto>>> Listar([FromQuery] EstadoConteo? estado, CancellationToken ct)
        => Ok(await _conteoService.ListarAsync(estado, User.ObtenerPaisId(), ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ConteosVer)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConteoDetalleDto>> Obtener(int id, CancellationToken ct)
        => Ok(await _conteoService.ObtenerAsync(id, User.ObtenerPaisId(), ct));

    [HttpPost]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(typeof(ConteoDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConteoDetalleDto>> Crear([FromBody] CrearConteoDto dto, CancellationToken ct)
    {
        var creado = await _conteoService.CrearAsync(dto, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Resumen.Id }, creado);
    }

    [HttpPut("{id:int}/cantidades")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDetalleDto>> GuardarCantidades(int id, [FromBody] GuardarCantidadesDto dto, CancellationToken ct)
        => Ok(await _conteoService.GuardarCantidadesAsync(id, dto, User.ObtenerPaisId(), UsuarioActual(), ct));

    [HttpGet("{id:int}/hoja")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarHoja(int id, CancellationToken ct)
    {
        var (contenido, nombreArchivo) = await _conteoService.GenerarHojaAsync(id, User.ObtenerPaisId(), ct);
        return File(contenido, ContentTypeXlsx, nombreArchivo);
    }

    // Sin [Consumes] a propósito: con él, la autorización no se evalúa antes de rechazar el
    // content-type (se vio en las pruebas multiusuario).
    [HttpPost("{id:int}/importar")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [RequestSizeLimit(MaxBytesArchivo + 1_000_000)]
    [ProducesResponseType(typeof(ImportarHojaConteoResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportarHojaConteoResultadoDto>> ImportarHoja(
        int id, IFormFile archivo, [FromQuery] bool adjuntar = false, CancellationToken ct = default)
    {
        var datos = await LeerArchivoAsync(archivo, "Subí el archivo de la hoja de conteo.", ct);
        var resultado = await _conteoService.ImportarHojaAsync(id, datos, archivo.FileName, adjuntar, User.ObtenerPaisId(), UsuarioActual(), ct);
        return Ok(resultado);
    }

    [HttpPost("{id:int}/evidencias")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [RequestSizeLimit(MaxBytesArchivo + 1_000_000)]
    [ProducesResponseType(typeof(ConteoEvidenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoEvidenciaDto>> AdjuntarEvidencia(int id, IFormFile archivo, CancellationToken ct)
    {
        var datos = await LeerArchivoAsync(archivo, "Subí el archivo de evidencia.", ct);
        var creada = await _conteoService.AdjuntarEvidenciaAsync(id, archivo.FileName, datos, User.ObtenerPaisId(), UsuarioActual(), ct);
        return StatusCode(StatusCodes.Status201Created, creada);
    }

    [HttpDelete("{id:int}/evidencias/{evidenciaId:int}")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EliminarEvidencia(int id, int evidenciaId, CancellationToken ct)
    {
        await _conteoService.EliminarEvidenciaAsync(id, evidenciaId, User.ObtenerPaisId(), UsuarioActual(), ct);
        return NoContent();
    }

    // Con autorización a propósito (a diferencia de la foto del producto): la evidencia es
    // un documento interno. El frontend la baja desde el servidor con el Bearer del usuario.
    [HttpGet("{id:int}/evidencias/{evidenciaId:int}")]
    [Authorize(Policy = Permisos.ConteosVer)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerEvidencia(int id, int evidenciaId, CancellationToken ct)
    {
        var (datos, contentType, nombre) = await _conteoService.ObtenerEvidenciaAsync(id, evidenciaId, User.ObtenerPaisId(), ct);
        return File(datos, contentType, nombre);
    }

    [HttpPost("{id:int}/cerrar")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDetalleDto>> Cerrar(int id, CancellationToken ct)
        => Ok(await _conteoService.CerrarAsync(id, User.ObtenerPaisId(), UsuarioActual(), ct));

    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDetalleDto>> Cancelar(int id, [FromBody] CancelarConteoDto dto, CancellationToken ct)
        => Ok(await _conteoService.CancelarAsync(id, dto, User.ObtenerPaisId(), UsuarioActual(), ct));

    [HttpPost("{id:int}/reconteo")]
    [Authorize(Policy = Permisos.ConteosRegistrar)]
    [ProducesResponseType(typeof(ConteoDetalleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ConteoDetalleDto>> CrearReconteo(int id, CancellationToken ct)
    {
        var creado = await _conteoService.CrearReconteoAsync(id, User.ObtenerPaisId(), UsuarioActual(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Resumen.Id }, creado);
    }

    private static async Task<byte[]> LeerArchivoAsync(IFormFile? archivo, string mensajeFaltante, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            throw new ArchivoInvalidoException(mensajeFaltante);
        if (archivo.Length > MaxBytesArchivo)
            throw new ArchivoInvalidoException("El archivo no puede superar los 10 MB.");

        using var ms = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    // Devuelve id + nombre del usuario logueado. El id sale del claim `sub`, que
    // llega mapeado a ClaimTypes.NameIdentifier — ver ClaimsPrincipalExtensions.
    private UsuarioActuante UsuarioActual() => User.ObtenerUsuarioActuante();
}
